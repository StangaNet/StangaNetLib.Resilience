using StangaNetLib.Core.Common;

namespace StangaNetLib.Resilience.Execution;

/// <summary>
/// Executes operations through named resilience pipelines, translating all failures
/// (retry exhaustion, circuit open, timeout, cancellation, unexpected exceptions) into
/// <see cref="Result"/> / <see cref="Result{T}"/> instead of throwing exceptions.
/// </summary>
/// <remarks>
/// Named pipelines are configured via <c>ResilienceSettings.Pipelines</c> in
/// <c>appsettings.json</c> and registered through
/// <see cref="Extensions.ServiceCollectionExtensions.AddStangaNetLibResilience(Microsoft.Extensions.DependencyInjection.IServiceCollection,Microsoft.Extensions.Configuration.IConfiguration)"/>.
/// Use the pipeline name <c>"Default"</c> to target the pre-configured default pipeline.
/// </remarks>
/// <example>
/// Injecting and using the executor:
/// <code>
/// public class OrderService
/// {
///     private readonly IResilientExecutor _resilience;
///
///     public OrderService(IResilientExecutor resilience) => _resilience = resilience;
///
///     public async Task&lt;Result&lt;OrderDto&gt;&gt; GetOrderAsync(Guid id, CancellationToken ct)
///         => await _resilience.ExecuteAsync("Default", async token =>
///         {
///             var order = await _db.Orders.FindAsync(id, token)
///                 ?? throw new InvalidOperationException($"Order {id} not found.");
///             return OrderDto.From(order);
///         }, ct);
/// }
/// </code>
/// </example>
public interface IResilientExecutor
{
    /// <summary>
    /// Executes <paramref name="operation"/> through the named resilience pipeline and returns
    /// <see cref="Result{T}.Success"/> on completion or a typed failure if the pipeline or
    /// the operation itself raises an exception.
    /// </summary>
    /// <typeparam name="T">The value type returned by the operation.</typeparam>
    /// <param name="pipelineName">
    /// Name of the resilience pipeline to use. Must match a key configured in
    /// <c>ResilienceSettings.Pipelines</c>. An unrecognised name executes without any resilience
    /// (pass-through behaviour from the underlying Polly registry).
    /// </param>
    /// <param name="operation">
    /// The async operation to execute. Receives a <see cref="CancellationToken"/> that merges
    /// the caller's token with any pipeline-level timeout.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the overall call.</param>
    /// <returns>
    /// <see cref="Result{T}.Success"/> wrapping the operation's return value, or
    /// <see cref="Result{T}.Failure(Error)"/> with one of the typed error codes defined in
    /// <see cref="Errors.ResilienceErrors"/>.
    /// </returns>
    Task<Result<T>> ExecuteAsync<T>(
        string pipelineName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a void <paramref name="operation"/> through the named resilience pipeline and
    /// returns <see cref="Result.Success()"/> on completion or a typed failure on any exception.
    /// </summary>
    /// <param name="pipelineName">Name of the resilience pipeline to use.</param>
    /// <param name="operation">The async void operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the overall call.</param>
    /// <returns>
    /// <see cref="Result.Success()"/> when the operation completes without error, or
    /// <see cref="Result.Failure(Error)"/> otherwise.
    /// </returns>
    Task<Result> ExecuteAsync(
        string pipelineName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
