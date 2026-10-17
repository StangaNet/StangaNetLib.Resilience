namespace StangaNetLib.Resilience.Settings;

/// <summary>Configuration for a single named resilience pipeline.</summary>
public sealed class ResiliencePipelineSettings
{
    /// <summary>
    /// Number of retry attempts after the initial failure.
    /// Set to 0 to disable retries entirely.
    /// Defaults to 3.
    /// </summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>
    /// Base delay between retry attempts.
    /// When <see cref="UseExponentialBackoff"/> is <see langword="true"/> this is the initial
    /// delay; subsequent attempts multiply it exponentially.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When <see langword="true"/> (default), retry delays grow exponentially with each attempt.
    /// When <see langword="false"/>, a constant delay equal to <see cref="RetryBaseDelay"/> is used.
    /// </summary>
    public bool UseExponentialBackoff { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/> (default), adds a small random jitter to each retry delay to
    /// prevent thundering-herd effects when many callers retry simultaneously.
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// Whether the circuit-breaker strategy is active for this pipeline.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool EnableCircuitBreaker { get; init; } = true;

    /// <summary>
    /// The ratio of failures within the sampling window that triggers the circuit to open.
    /// Must be in [0.0, 1.0]. For example, 0.5 means 50 % of calls must fail.
    /// Defaults to 0.5.
    /// </summary>
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    /// <summary>
    /// Minimum number of calls that must occur within the sampling window before the
    /// circuit-breaker can evaluate and potentially open.
    /// Defaults to 10.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    /// <summary>
    /// Duration of the sliding window used to measure the failure ratio.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Duration the circuit stays open (fully rejecting all calls) before transitioning to
    /// half-open and attempting a probe request.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether the per-attempt timeout strategy is active for this pipeline.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool EnableTimeout { get; init; } = true;

    /// <summary>
    /// Maximum allowed duration for each individual attempt (including the initial call and
    /// every retry). The timeout is applied independently to each attempt, not to the total
    /// execution.
    /// Defaults to 10 seconds.
    /// </summary>
    public TimeSpan TimeoutPerAttempt { get; init; } = TimeSpan.FromSeconds(10);
}
