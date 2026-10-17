using StangaNetLib.Core.Validators;

namespace StangaNetLib.Resilience.Settings;

internal sealed class ResilienceSettingsValidator : SettingsValidatorBase<ResilienceSettings>
{
    protected override void ValidateCore(ResilienceSettings options, List<string> errors)
    {
        foreach (var (pipelineName, pipeline) in options.Pipelines)
        {
            if (pipeline.RetryCount < 0)
                errors.Add($"Pipeline '{pipelineName}': RetryCount must be >= 0.");

            if (pipeline.RetryBaseDelay < TimeSpan.Zero)
                errors.Add($"Pipeline '{pipelineName}': RetryBaseDelay must be >= 0.");

            if (pipeline.CircuitBreakerFailureRatio is < 0.0 or > 1.0)
                errors.Add($"Pipeline '{pipelineName}': CircuitBreakerFailureRatio must be in [0.0, 1.0].");

            if (pipeline.CircuitBreakerMinimumThroughput <= 0)
                errors.Add($"Pipeline '{pipelineName}': CircuitBreakerMinimumThroughput must be > 0.");

            if (pipeline.CircuitBreakerSamplingDuration <= TimeSpan.Zero)
                errors.Add($"Pipeline '{pipelineName}': CircuitBreakerSamplingDuration must be > 0.");

            if (pipeline.CircuitBreakerBreakDuration <= TimeSpan.Zero)
                errors.Add($"Pipeline '{pipelineName}': CircuitBreakerBreakDuration must be > 0.");

            // Polly 8.x enforces a minimum timeout of 10 ms (Range: 00:00:00.0100000 to 1.00:00:00).
            if (pipeline.EnableTimeout && pipeline.TimeoutPerAttempt < TimeSpan.FromMilliseconds(10))
                errors.Add($"Pipeline '{pipelineName}': TimeoutPerAttempt must be >= 10 ms when EnableTimeout is true.");
        }
    }
}
