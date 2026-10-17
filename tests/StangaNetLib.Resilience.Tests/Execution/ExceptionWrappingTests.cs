using Microsoft.Extensions.DependencyInjection;
using Polly;
using StangaNetLib.Core.Common;
using StangaNetLib.Resilience.Execution;

namespace StangaNetLib.Resilience.Tests.Execution;

public sealed class ExceptionWrappingTests
{
    private static IResilientExecutor BuildExecutor()
    {
        var services = new ServiceCollection();
        services.AddResiliencePipeline("p", (_) => { }); // empty pipeline: no retry, no circuit, no timeout
        services.AddSingleton<IResilientExecutor, ResilientExecutor>();
        return services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();
    }

    // ── Exception types all produce ExecutionFailed ──────────────────────────

    [Fact]
    public async Task ExecuteAsync_AggregateException_WrappedAsExecutionFailed()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new AggregateException("outer", new InvalidOperationException("inner")));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    [Fact]
    public async Task ExecuteAsync_ApplicationException_WrappedAsExecutionFailed()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new ApplicationException("app exception"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    [Fact]
    public async Task ExecuteAsync_ObjectDisposedException_WrappedAsExecutionFailed()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new ObjectDisposedException("SomeResource"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
        result.Error.Type.Should().Be(ErrorType.Internal);
    }

    [Fact]
    public async Task ExecuteAsync_ArgumentException_WrappedAsExecutionFailed()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new ArgumentException("bad arg", "paramName"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    [Fact]
    public async Task ExecuteAsync_NullReferenceException_WrappedAsExecutionFailed()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new NullReferenceException("null ref"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    // ── Error description content ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Exception_ErrorDescriptionContainsExceptionMessage()
    {
        var executor = BuildExecutor();
        const string ExceptionMessage = "specific-failure-detail-xyz";

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new InvalidOperationException(ExceptionMessage));

        result.Error.Description.Should().Contain(ExceptionMessage);
    }

    [Fact]
    public async Task ExecuteAsync_Exception_ErrorDescriptionContainsPipelineName()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync<string>("p",
            _ => throw new InvalidOperationException("fail"));

        result.Error.Description.Should().Contain("p");
    }

    // ── Void overload wraps all exceptions ────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_VoidOverload_ApplicationException_WrappedAsExecutionFailed()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync("p",
            _ => throw new ApplicationException("void fail"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    [Fact]
    public async Task ExecuteAsync_VoidOverload_NullReferenceException_WrappedAsExecutionFailed()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync("p",
            _ => throw new NullReferenceException("null ref"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Resilience.ExecutionFailed");
    }

    // ── No exception escapes as thrown ────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_VariousExceptionTypes_NoneEscapeAsThrown()
    {
        var executor = BuildExecutor();
        var exceptions = new Exception[]
        {
            new InvalidOperationException("ioe"),
            new ArgumentNullException("param"),
            new FormatException("fmt"),
            new DivideByZeroException(),
            new OverflowException("overflow"),
        };

        foreach (var ex in exceptions)
        {
            var capturedEx = ex;
            var result = await executor.ExecuteAsync<string>("p", _ => throw capturedEx);
            result.IsFailure.Should().BeTrue($"{capturedEx.GetType().Name} must be wrapped, not rethrown");
            result.Error.Code.Should().Be("Resilience.ExecutionFailed");
        }
    }
}
