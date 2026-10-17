using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using StangaNetLib.Resilience.Execution;

namespace StangaNetLib.Resilience.Tests.Execution;

public sealed class ComposedPipelineTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static IResilientExecutor BuildExecutor(string name, Action<ResiliencePipelineBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddResiliencePipeline(name, configure);
        services.AddSingleton<IResilientExecutor, ResilientExecutor>();
        return services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();
    }

    private static IResilientExecutor BuildRetryThenCircuitBreaker(
        string name,
        int maxRetries,
        int cbMinThroughput)
        => BuildExecutor(name, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.Zero,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                MinimumThroughput = cbMinThroughput,
                FailureRatio = 1.0,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
            }));

    private static IResilientExecutor BuildRetryThenTimeout(
        string name,
        int maxRetries,
        TimeSpan timeout)
        => BuildExecutor(name, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.Zero,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout,
            }));

    // ── Retry (outer) + CircuitBreaker (inner) ────────────────────────────────
    // Retry wraps CB: each retry attempt is a separate call seen by the CB.
    // CB opens once MinimumThroughput failures accumulate; subsequent retry
    // attempts hit an open circuit and receive BrokenCircuitException immediately
    // without invoking the operation delegate.

    [Fact]
    public async Task RetryThenCircuitBreaker_AlwaysFailing_CircuitOpensWithinRetryLoop_FinalErrorIsCircuitOpen()
    {
        // CB threshold = 2 failures → opens during the retry loop.
        // Remaining retries hit the open circuit.
        // After retries exhausted, BrokenCircuitException propagates → CircuitOpen.
        var executor = BuildRetryThenCircuitBreaker("p", maxRetries: 4, cbMinThroughput: 2);

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new InvalidOperationException("always fails"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.CircuitOpen");
    }

    [Fact]
    public async Task RetryThenCircuitBreaker_CircuitOpens_OperationInvocationsCapAtThreshold()
    {
        // Once the circuit opens, BrokenCircuitException is thrown before the
        // operation delegate is invoked. Invocation count must equal cbMinThroughput.
        var invocations = 0;
        var executor = BuildRetryThenCircuitBreaker("p", maxRetries: 6, cbMinThroughput: 2);

        await executor.ExecuteAsync<string>("p", _ =>
        {
            invocations++;
            throw new InvalidOperationException("fail");
        });

        invocations.Should().Be(2, "operation must not be invoked after the circuit opens");
    }

    [Fact]
    public async Task RetryThenCircuitBreaker_SucceedsBeforeThreshold_ReturnsSuccess()
    {
        // cbMinThroughput=4 means 2 failures are not enough to open the circuit.
        // The 3rd attempt succeeds — the composed pipeline returns a success result.
        var attempts = 0;
        var executor = BuildRetryThenCircuitBreaker("p", maxRetries: 3, cbMinThroughput: 4);

        var result = await executor.ExecuteAsync("p", _ =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("not yet");
            return Task.FromResult("recovered");
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("recovered");
    }

    // ── Retry (outer) + Timeout (inner) ──────────────────────────────────────
    // Timeout is applied independently to each attempt. TimeoutRejectedException
    // (or its OCE variant) propagates to the Retry, which retries the next attempt.
    // When retries are exhausted, the Timeout error surfaces.

    [Fact]
    public async Task RetryThenTimeout_AllAttemptsExceedTimeout_FinalErrorIsTimeout()
    {
        // Every attempt runs indefinitely → every attempt times out.
        // After retries exhausted, Timeout error is returned (never ExecutionFailed).
        var executor = BuildRetryThenTimeout("p", maxRetries: 2, timeout: TimeSpan.FromMilliseconds(100));

        var result = await executor.ExecuteAsync("p", async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return "never";
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.Timeout");
    }

    [Fact]
    public async Task RetryThenTimeout_FirstAttemptTimesOut_SubsequentAttemptSucceeds_ReturnsSuccess()
    {
        // The first attempt is slow and times out; the second attempt is fast → success.
        var attempts = 0;
        var executor = BuildRetryThenTimeout("p", maxRetries: 2, timeout: TimeSpan.FromMilliseconds(100));

        var result = await executor.ExecuteAsync("p", async ct =>
        {
            attempts++;
            if (attempts == 1)
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return "recovered";
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("recovered");
    }

    [Fact]
    public async Task RetryThenTimeout_TotalAttemptCountEqualsMaxRetriesPlusOne()
    {
        // Each timeout triggers a retry; total invocations must be 1 initial + MaxRetryAttempts.
        var attempts = 0;
        var executor = BuildRetryThenTimeout("p", maxRetries: 2, timeout: TimeSpan.FromMilliseconds(100));

        await executor.ExecuteAsync("p", async ct =>
        {
            attempts++;
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return "never";
        });

        attempts.Should().Be(3, "1 initial attempt + 2 retries, each timed out independently");
    }
}
