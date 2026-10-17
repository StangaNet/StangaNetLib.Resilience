using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using StangaNetLib.Core.Common;
using StangaNetLib.Resilience.Execution;
using StangaNetLib.Resilience.Settings;

namespace StangaNetLib.Resilience.Tests.Execution;

public sealed class ResilientExecutorTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static IResilientExecutor BuildExecutor(
        string pipelineName,
        Action<ResiliencePipelineBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddResiliencePipeline(pipelineName, builder =>
            configure?.Invoke(builder));
        services.AddSingleton<IResilientExecutor, ResilientExecutor>();
        return services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();
    }

    private static IResilientExecutor BuildRetryExecutor(string name, int retries) =>
        BuildExecutor(name, builder => builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = retries,
            Delay = TimeSpan.Zero,
            BackoffType = DelayBackoffType.Constant,
            UseJitter = false,
        }));

    private static IResilientExecutor BuildCircuitBreakerExecutor(string name) =>
        BuildExecutor(name, builder => builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            MinimumThroughput = 2,
            FailureRatio = 1.0,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30),
        }));

    private static IResilientExecutor BuildTimeoutExecutor(string name, TimeSpan timeout) =>
        BuildExecutor(name, builder => builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = timeout,
        }));

    // ── ExecuteAsync<T> — happy path ─────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ReturnsSuccessWithValue()
    {
        var executor = BuildExecutor("p");

        var result = await executor.ExecuteAsync("p", _ => Task.FromResult(42));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_PassesCancellationToken()
    {
        var executor = BuildExecutor("p");
        using var cts = new CancellationTokenSource();
        CancellationToken received = default;

        await executor.ExecuteAsync("p", ct =>
        {
            received = ct;
            return Task.FromResult(true);
        }, cts.Token);

        received.Should().NotBe(default(CancellationToken));
    }

    // ── ExecuteAsync<T> — failure paths ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_OperationThrows_ReturnsFailure()
    {
        var executor = BuildExecutor("p");

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new InvalidOperationException("boom"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OperationThrows_ErrorCodeIsExecutionFailed()
    {
        var executor = BuildExecutor("p");

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new InvalidOperationException("boom"));

        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
        result.Error.Type.Should().Be(ErrorType.Internal);
        result.Error.Description.Should().Contain("boom");
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_ReturnsCancelledError()
    {
        var executor = BuildExecutor("p");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await executor.ExecuteAsync<string>("p",
            ct => Task.FromCanceled<string>(ct), cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.Cancelled");
    }

    // ── ExecuteAsync<T> — retry ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RetryPolicy_EventuallySucceeds()
    {
        var executor = BuildRetryExecutor("p", retries: 3);
        var attempts = 0;

        var result = await executor.ExecuteAsync("p", _ =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("not yet");
            return Task.FromResult("ok");
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_RetryPolicy_ExhaustedReturnsFailure()
    {
        var executor = BuildRetryExecutor("p", retries: 2);

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new InvalidOperationException("always fails"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    // ── ExecuteAsync<T> — circuit breaker ────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CircuitBreakerOpen_ReturnsCircuitOpenError()
    {
        var executor = BuildCircuitBreakerExecutor("p");

        // Drive 2 failures to open the circuit (MinimumThroughput=2, FailureRatio=1.0)
        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException("fail"));
        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException("fail"));

        // Circuit is now open — next call is rejected immediately
        var result = await executor.ExecuteAsync<string>("p", _ => Task.FromResult("should-not-reach"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.CircuitOpen");
        result.Error.Type.Should().Be(ErrorType.Internal);
    }

    // ── ExecuteAsync<T> — timeout ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Timeout_ReturnsTimeoutError()
    {
        // Polly 8.x minimum timeout is 10 ms; 100 ms is used here to avoid flakiness.
        var executor = BuildTimeoutExecutor("p", TimeSpan.FromMilliseconds(100));

        var result = await executor.ExecuteAsync("p", async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return "never";
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.Timeout");
        result.Error.Type.Should().Be(ErrorType.Internal);
    }

    // ── ExecuteAsync (void) ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_VoidOperation_Succeeds()
    {
        var executor = BuildExecutor("p");
        var invoked = false;

        var result = await executor.ExecuteAsync("p", _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        result.IsSuccess.Should().BeTrue();
        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_VoidOperation_Throws_ReturnsFailure()
    {
        var executor = BuildExecutor("p");

        var result = await executor.ExecuteAsync("p",
            _ => throw new InvalidOperationException("boom"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    [Fact]
    public async Task ExecuteAsync_VoidOperation_CircuitOpen_ReturnsCircuitOpenError()
    {
        var executor = BuildCircuitBreakerExecutor("p");

        await executor.ExecuteAsync("p", _ => throw new InvalidOperationException("fail"));
        await executor.ExecuteAsync("p", _ => throw new InvalidOperationException("fail"));

        var result = await executor.ExecuteAsync("p", _ => Task.CompletedTask);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.CircuitOpen");
    }

    // ── Guard clause tests ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        var act = () => new ResilientExecutor(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("provider");
    }

    [Fact]
    public async Task ExecuteAsync_NullPipelineName_Throws()
    {
        var executor = BuildExecutor("p");

        var act = async () => await executor.ExecuteAsync<string>(
            null!, _ => Task.FromResult("x"));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullOperation_Throws()
    {
        var executor = BuildExecutor("p");

        var act = async () => await executor.ExecuteAsync<string>(
            "p", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhitespacePipelineName_Throws()
    {
        var executor = BuildExecutor("p");

        var act = async () => await executor.ExecuteAsync<string>(
            "   ", _ => Task.FromResult("x"));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
