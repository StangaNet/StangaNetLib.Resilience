using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Resilience.Errors;

namespace StangaNetLib.Resilience.Execution;

internal sealed class ResilientExecutor : IResilientExecutor
{
    private readonly ResiliencePipelineProvider<string> _provider;

    public ResilientExecutor(ResiliencePipelineProvider<string> provider)
    {
        _provider = Guard.Against.Null(provider, nameof(provider));
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        string pipelineName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        Guard.Against.Null(operation, nameof(operation));

        try
        {
            var pipeline = _provider.GetPipeline(pipelineName);
            var value = await pipeline.ExecuteAsync(
                async ct => await operation(ct),
                cancellationToken);
            return Result<T>.Success(value);
        }
        catch (BrokenCircuitException ex)
        {
            return Result<T>.Failure(ResilienceErrors.CircuitOpen(pipelineName, ex.Message));
        }
        catch (TimeoutRejectedException ex)
        {
            return Result<T>.Failure(ResilienceErrors.Timeout(pipelineName, ex.Message));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<T>.Failure(ResilienceErrors.Cancelled(pipelineName));
        }
        catch (OperationCanceledException ex)
        {
            // Polly's timeout strategy cancels an internal linked token; the resulting
            // OperationCanceledException reaches here when the outer token is not cancelled.
            return Result<T>.Failure(ResilienceErrors.Timeout(pipelineName, ex.Message));
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ResilienceErrors.ExecutionFailed(pipelineName, ex));
        }
    }

    public async Task<Result> ExecuteAsync(
        string pipelineName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        Guard.Against.Null(operation, nameof(operation));

        try
        {
            var pipeline = _provider.GetPipeline(pipelineName);
            await pipeline.ExecuteAsync(
                async ct => await operation(ct),
                cancellationToken);
            return Result.Success();
        }
        catch (BrokenCircuitException ex)
        {
            return Result.Failure(ResilienceErrors.CircuitOpen(pipelineName, ex.Message));
        }
        catch (TimeoutRejectedException ex)
        {
            return Result.Failure(ResilienceErrors.Timeout(pipelineName, ex.Message));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(ResilienceErrors.Cancelled(pipelineName));
        }
        catch (OperationCanceledException ex)
        {
            return Result.Failure(ResilienceErrors.Timeout(pipelineName, ex.Message));
        }
        catch (Exception ex)
        {
            return Result.Failure(ResilienceErrors.ExecutionFailed(pipelineName, ex));
        }
    }
}
