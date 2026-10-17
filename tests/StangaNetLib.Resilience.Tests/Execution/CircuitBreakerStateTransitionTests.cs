using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using StangaNetLib.Resilience.Execution;

namespace StangaNetLib.Resilience.Tests.Execution;

public sealed class CircuitBreakerStateTransitionTests
{
    private static IResilientExecutor BuildCircuitBreaker(string name, TimeSpan? breakDuration = null)
    {
        var services = new ServiceCollection();
        services.AddResiliencePipeline(name, builder => builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            MinimumThroughput = 2,
            FailureRatio = 1.0,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = breakDuration ?? TimeSpan.FromSeconds(30),
        }));
        services.AddSingleton<IResilientExecutor, ResilientExecutor>();
        return services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();
    }

    // ── Closed state (fresh circuit) ──────────────────────────────────────────

    [Fact]
    public async Task CircuitBreaker_FreshExecutor_StartsInClosedState()
    {
        var executor = BuildCircuitBreaker("p");

        var result = await executor.ExecuteAsync("p", _ => Task.FromResult("ok"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }

    // ── Closed → Open transition ───────────────────────────────────────────────

    [Fact]
    public async Task CircuitBreaker_AfterMinimumFailures_OpensCircuit()
    {
        var executor = BuildCircuitBreaker("p");

        // MinimumThroughput = 2 with FailureRatio = 1.0 → 2 failures open the circuit
        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException("fail 1"));
        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException("fail 2"));

        var result = await executor.ExecuteAsync<string>("p", _ => Task.FromResult("probe"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.CircuitOpen");
    }

    [Fact]
    public async Task CircuitBreaker_OpenCircuit_RejectsWithoutInvokingOperation()
    {
        var executor = BuildCircuitBreaker("p");
        var invocations = 0;

        await executor.ExecuteAsync<string>("p", _ => { invocations++; throw new InvalidOperationException(); });
        await executor.ExecuteAsync<string>("p", _ => { invocations++; throw new InvalidOperationException(); });

        invocations = 0; // reset counter; next call should be rejected without delegate invocation
        await executor.ExecuteAsync<string>("p", _ => { invocations++; return Task.FromResult("x"); });

        invocations.Should().Be(0, "open circuit must reject before invoking the operation");
    }

    [Fact]
    public async Task CircuitBreaker_OpenCircuit_ErrorTypeIsInternal()
    {
        var executor = BuildCircuitBreaker("p");

        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException());

        var result = await executor.ExecuteAsync<string>("p", _ => Task.FromResult("x"));

        result.Error.Type.Should().Be(StangaNetLib.Core.Common.ErrorType.Internal);
    }

    // ── Open → Half-open → Closed transition ─────────────────────────────────
    // Conservative break duration (500 ms) with a 3× wait avoids OS scheduler jitter.

    [Fact]
    public async Task CircuitBreaker_AfterBreakDuration_AllowsProbeCall()
    {
        var executor = BuildCircuitBreaker("p", breakDuration: TimeSpan.FromMilliseconds(500));

        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException());

        // Wait well past the break duration so the circuit transitions to half-open.
        await Task.Delay(1600);

        // Half-open: probe call is attempted. A successful probe closes the circuit.
        var probeResult = await executor.ExecuteAsync("p", _ => Task.FromResult("probe-ok"));

        probeResult.IsSuccess.Should().BeTrue("a successful probe after break duration should be accepted");
    }

    [Fact]
    public async Task CircuitBreaker_SuccessfulProbeAfterBreak_ClosesCircuitForSubsequentCalls()
    {
        var executor = BuildCircuitBreaker("p", breakDuration: TimeSpan.FromMilliseconds(500));

        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync<string>("p", _ => throw new InvalidOperationException());

        await Task.Delay(1600);

        // Successful probe transitions circuit to closed.
        await executor.ExecuteAsync("p", _ => Task.FromResult("probe"));

        // Subsequent calls must execute normally.
        var afterResult = await executor.ExecuteAsync("p", _ => Task.FromResult("after-recovery"));

        afterResult.IsSuccess.Should().BeTrue("circuit should be closed after a successful probe");
    }

    // ── Void overload ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CircuitBreaker_VoidOverload_OpenCircuit_ReturnsCircuitOpenError()
    {
        var executor = BuildCircuitBreaker("p");

        await executor.ExecuteAsync("p", _ => throw new InvalidOperationException());
        await executor.ExecuteAsync("p", _ => throw new InvalidOperationException());

        var result = await executor.ExecuteAsync("p", _ => Task.CompletedTask);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.CircuitOpen");
    }
}
