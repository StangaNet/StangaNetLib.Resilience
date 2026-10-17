using StangaNetLib.Core.Common;

namespace StangaNetLib.Resilience.Errors;

/// <summary>
/// Pre-defined <see cref="Error"/> factory methods for resilience pipeline failures.
/// Each error has a machine-readable <c>Resilience.*</c> code and an <see cref="ErrorType.Internal"/> type
/// so consumers can branch on the code without parsing the description.
/// </summary>
public static class ResilienceErrors
{
    /// <summary>
    /// The circuit breaker for <paramref name="pipelineName"/> is open — the operation was
    /// rejected immediately without attempting execution.
    /// Error code: <c>Resilience.CircuitOpen</c>.
    /// </summary>
    public static Error CircuitOpen(string pipelineName, string detail)
        => new(
            "Resilience.CircuitOpen",
            $"The circuit breaker for pipeline '{pipelineName}' is open. Operation rejected. Detail: {detail}",
            ErrorType.Internal);

    /// <summary>
    /// The operation exceeded its configured per-attempt timeout.
    /// Error code: <c>Resilience.Timeout</c>.
    /// </summary>
    public static Error Timeout(string pipelineName, string detail)
        => new(
            "Resilience.Timeout",
            $"The operation in pipeline '{pipelineName}' exceeded the configured timeout. Detail: {detail}",
            ErrorType.Internal);

    /// <summary>
    /// The operation was cancelled via the caller's <see cref="CancellationToken"/>.
    /// Error code: <c>Resilience.Cancelled</c>.
    /// </summary>
    public static Error Cancelled(string pipelineName)
        => new(
            "Resilience.Cancelled",
            $"The operation in pipeline '{pipelineName}' was cancelled.",
            ErrorType.Internal);

    /// <summary>
    /// The operation failed after all retry attempts were exhausted, or an unexpected exception
    /// was thrown and the pipeline did not rethrow a more specific resilience exception.
    /// Error code: <c>Resilience.ExecutionFailed</c>.
    /// </summary>
    public static Error ExecutionFailed(string pipelineName, Exception exception)
        => new(
            "Resilience.ExecutionFailed",
            $"The operation in pipeline '{pipelineName}' failed: {exception.Message}",
            ErrorType.Internal);
}
