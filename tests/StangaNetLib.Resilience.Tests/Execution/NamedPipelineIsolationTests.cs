using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StangaNetLib.Core.Common;
using StangaNetLib.Resilience.Execution;

namespace StangaNetLib.Resilience.Tests.Execution;

public sealed class NamedPipelineIsolationTests
{
    private static IResilientExecutor BuildExecutor(params (string name, Action<ResiliencePipelineBuilder> configure)[] pipelines)
    {
        var services = new ServiceCollection();
        foreach (var (name, configure) in pipelines)
            services.AddResiliencePipeline(name, configure);
        services.AddSingleton<IResilientExecutor, ResilientExecutor>();
        return services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();
    }

    private static Action<ResiliencePipelineBuilder> CircuitBreakerOnly() =>
        b => b.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            MinimumThroughput = 2,
            FailureRatio = 1.0,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(30),
        });

    private static Action<ResiliencePipelineBuilder> RetryOnly(int maxAttempts) =>
        b => b.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = maxAttempts,
            Delay = TimeSpan.Zero,
            BackoffType = DelayBackoffType.Constant,
            UseJitter = false,
        });

    // ── Basic independence ─────────────────────────────────────────────────────

    [Fact]
    public async Task TwoPipelines_BothExecuteSuccessfully()
    {
        var executor = BuildExecutor(("Alpha", _ => { }), ("Beta", _ => { }));

        var alphaResult = await executor.ExecuteAsync("Alpha", _ => Task.FromResult("alpha"));
        var betaResult  = await executor.ExecuteAsync("Beta",  _ => Task.FromResult("beta"));

        alphaResult.IsSuccess.Should().BeTrue();
        alphaResult.Value.Should().Be("alpha");
        betaResult.IsSuccess.Should().BeTrue();
        betaResult.Value.Should().Be("beta");
    }

    // ── Circuit-breaker isolation ──────────────────────────────────────────────

    [Fact]
    public async Task TwoPipelines_OpeningCircuitOnOneDoesNotAffectOther()
    {
        var executor = BuildExecutor(("A", CircuitBreakerOnly()), ("B", CircuitBreakerOnly()));

        // Drive pipeline "A" to open
        await executor.ExecuteAsync<string>("A", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync<string>("A", _ => throw new InvalidOperationException());

        var aResult = await executor.ExecuteAsync<string>("A", _ => Task.FromResult("x"));
        aResult.Error.Code.Should().Be("Resilience.CircuitOpen");

        // Pipeline "B" must still accept calls
        var bResult = await executor.ExecuteAsync("B", _ => Task.FromResult("b-ok"));
        bResult.IsSuccess.Should().BeTrue("pipeline B must be isolated from pipeline A's circuit state");
    }

    [Fact]
    public async Task TwoPipelines_FailingOnBothIndependently_EachOpensItsOwnCircuit()
    {
        var executor = BuildExecutor(("P1", CircuitBreakerOnly()), ("P2", CircuitBreakerOnly()));

        await executor.ExecuteAsync<string>("P1", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync<string>("P1", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync<string>("P2", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync<string>("P2", _ => throw new InvalidOperationException());

        var p1Result = await executor.ExecuteAsync<string>("P1", _ => Task.FromResult("x"));
        var p2Result = await executor.ExecuteAsync<string>("P2", _ => Task.FromResult("x"));

        p1Result.Error.Code.Should().Be("Resilience.CircuitOpen");
        p2Result.Error.Code.Should().Be("Resilience.CircuitOpen");
    }

    // ── Retry isolation ───────────────────────────────────────────────────────

    [Fact]
    public async Task TwoPipelines_SeparateRetryConfigurations_EachRespectsItsOwnRetryCount()
    {
        var attemptsA = 0;
        var attemptsB = 0;

        var executor = BuildExecutor(
            ("RetryA", RetryOnly(maxAttempts: 2)),
            ("RetryB", RetryOnly(maxAttempts: 4)));

        await executor.ExecuteAsync<string>("RetryA", _ => { attemptsA++; throw new InvalidOperationException(); });
        await executor.ExecuteAsync<string>("RetryB", _ => { attemptsB++; throw new InvalidOperationException(); });

        attemptsA.Should().Be(3, "RetryA: 1 initial + 2 retries");
        attemptsB.Should().Be(5, "RetryB: 1 initial + 4 retries");
    }

    // ── Error description includes pipeline name ───────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailedOperation_ErrorDescriptionNamesPipelineThatFailed()
    {
        var executor = BuildExecutor(("AlphaPipeline", _ => { }), ("BetaPipeline", _ => { }));

        var result = await executor.ExecuteAsync<string>("AlphaPipeline",
            _ => throw new InvalidOperationException("boom"));

        result.Error.Description.Should().Contain("AlphaPipeline");
        result.Error.Description.Should().NotContain("BetaPipeline");
    }

    // ── Unregistered pipeline name ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_UnregisteredPipelineName_ReturnsFailureNotThrows()
    {
        var executor = BuildExecutor(("Registered", _ => { }));

        // Must not throw — executor must return a failure Result
        var result = await executor.ExecuteAsync<string>("Unregistered", _ => Task.FromResult("x"));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Internal);
    }
}
