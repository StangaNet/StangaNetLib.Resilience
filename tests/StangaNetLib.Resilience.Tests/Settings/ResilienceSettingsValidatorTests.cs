using Microsoft.Extensions.Options;
using StangaNetLib.Resilience.Settings;

namespace StangaNetLib.Resilience.Tests.Settings;

public sealed class ResilienceSettingsValidatorTests
{
    private static readonly ResilienceSettingsValidator Validator = new();

    private static ValidateOptionsResult Validate(ResilienceSettings settings) =>
        Validator.Validate(null, settings);

    // ── Valid settings ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultSettings_Succeeds()
    {
        var result = Validate(new ResilienceSettings());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultiplePipelines_AllValid_Succeeds()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["Http"] = new ResiliencePipelineSettings { RetryCount = 5 },
                ["Database"] = new ResiliencePipelineSettings { RetryCount = 0 }
            }
        };

        var result = Validate(settings);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_RetryCountZero_Succeeds()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines = { ["p"] = new ResiliencePipelineSettings { RetryCount = 0 } }
        };

        Validate(settings).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_CircuitBreakerDisabled_TimeoutDisabled_Succeeds()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings
                {
                    EnableCircuitBreaker = false,
                    EnableTimeout = false
                }
            }
        };

        Validate(settings).Succeeded.Should().BeTrue();
    }

    // ── Invalid settings ──────────────────────────────────────────────────────

    [Fact]
    public void Validate_NegativeRetryCount_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines = { ["p"] = new ResiliencePipelineSettings { RetryCount = -1 } }
        };

        var result = Validate(settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(m => m.Contains("RetryCount"));
    }

    [Fact]
    public void Validate_NegativeRetryBaseDelay_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings { RetryBaseDelay = TimeSpan.FromSeconds(-1) }
            }
        };

        var result = Validate(settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(m => m.Contains("RetryBaseDelay"));
    }

    [Fact]
    public void Validate_CircuitBreakerFailureRatioAboveOne_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings { CircuitBreakerFailureRatio = 1.5 }
            }
        };

        var result = Validate(settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(m => m.Contains("CircuitBreakerFailureRatio"));
    }

    [Fact]
    public void Validate_CircuitBreakerFailureRatioNegative_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings { CircuitBreakerFailureRatio = -0.1 }
            }
        };

        Validate(settings).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_CircuitBreakerMinimumThroughputZero_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings { CircuitBreakerMinimumThroughput = 0 }
            }
        };

        var result = Validate(settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(m => m.Contains("CircuitBreakerMinimumThroughput"));
    }

    [Fact]
    public void Validate_CircuitBreakerSamplingDurationZero_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings { CircuitBreakerSamplingDuration = TimeSpan.Zero }
            }
        };

        Validate(settings).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_CircuitBreakerBreakDurationZero_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings { CircuitBreakerBreakDuration = TimeSpan.Zero }
            }
        };

        Validate(settings).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_TimeoutEnabledBelowMinimum_Fails()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["p"] = new ResiliencePipelineSettings
                {
                    EnableTimeout = true,
                    TimeoutPerAttempt = TimeSpan.FromMilliseconds(5) // below 10 ms minimum
                }
            }
        };

        var result = Validate(settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(m => m.Contains("TimeoutPerAttempt"));
    }

    [Fact]
    public void Validate_MultiplePipelinesOneInvalid_ReportsCorrectPipelineName()
    {
        var settings = new ResilienceSettings
        {
            InternalPipelines =
            {
                ["Good"] = new ResiliencePipelineSettings(),
                ["Bad"] = new ResiliencePipelineSettings { RetryCount = -1 }
            }
        };

        var result = Validate(settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(m => m.Contains("Bad"));
        result.Failures.Should().NotContain(m => m.Contains("Good"));
    }
}
