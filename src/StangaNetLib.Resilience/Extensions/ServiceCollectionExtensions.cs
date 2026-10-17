using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using StangaNetLib.Resilience.Execution;
using StangaNetLib.Resilience.Settings;

namespace StangaNetLib.Resilience.Extensions;

/// <summary>
/// Extension methods for registering StangaNetLib.Resilience services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IResilientExecutor"/> and all named resilience pipelines defined in
    /// the <c>"ResilienceSettings"</c> configuration section.
    /// Also registers <see cref="IOptions{ResilienceSettings}"/> for consumers that need dynamic
    /// access to settings, and <see cref="IValidateOptions{TOptions}"/> for startup validation.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Application configuration used to bind the <c>"ResilienceSettings"</c> section.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// appsettings.json:
    /// <code>
    /// {
    ///   "ResilienceSettings": {
    ///     "Pipelines": {
    ///       "Default": {
    ///         "RetryCount": 3,
    ///         "RetryBaseDelay": "00:00:01",
    ///         "UseExponentialBackoff": true,
    ///         "EnableCircuitBreaker": true,
    ///         "CircuitBreakerFailureRatio": 0.5,
    ///         "CircuitBreakerMinimumThroughput": 10,
    ///         "CircuitBreakerSamplingDuration": "00:00:30",
    ///         "CircuitBreakerBreakDuration": "00:00:30",
    ///         "EnableTimeout": true,
    ///         "TimeoutPerAttempt": "00:00:10"
    ///       },
    ///       "Http": {
    ///         "RetryCount": 5,
    ///         "TimeoutPerAttempt": "00:00:30"
    ///       }
    ///     }
    ///   }
    /// }
    /// </code>
    /// Program.cs:
    /// <code>
    /// builder.Services.AddStangaNetLibResilience(builder.Configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddStangaNetLibResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ResilienceSettings>(configuration.GetSection(ResilienceSettings.SectionName));
        services.AddSingleton<IValidateOptions<ResilienceSettings>, ResilienceSettingsValidator>();

        var settings = configuration
            .GetSection(ResilienceSettings.SectionName)
            .Get<ResilienceSettings>() ?? new ResilienceSettings();

        return RegisterPipelines(services, settings);
    }

    /// <summary>
    /// Registers <see cref="IResilientExecutor"/> using a direct configuration delegate.
    /// Use for testing or scenarios where no configuration file is available.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Delegate to configure <see cref="ResilienceSettings"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddStangaNetLibResilience(options =>
    /// {
    ///     options.Pipelines["Default"] = new ResiliencePipelineSettings
    ///     {
    ///         RetryCount = 5,
    ///         TimeoutPerAttempt = TimeSpan.FromSeconds(30)
    ///     };
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddStangaNetLibResilience(
        this IServiceCollection services,
        Action<ResilienceSettings> configure)
    {
        var settings = new ResilienceSettings();
        configure(settings);
        services.Configure<ResilienceSettings>(configure);
        services.AddSingleton<IValidateOptions<ResilienceSettings>, ResilienceSettingsValidator>();
        return RegisterPipelines(services, settings);
    }

    /// <summary>
    /// Registers <see cref="IResilientExecutor"/> with a single <c>"Default"</c> pipeline using
    /// production-safe defaults (3 retries, exponential backoff, circuit breaker, 10 s timeout).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibResilience(this IServiceCollection services)
        => services.AddStangaNetLibResilience(_ => { });

    private static IServiceCollection RegisterPipelines(IServiceCollection services, ResilienceSettings settings)
    {
        if (settings.Pipelines.Count == 0)
            settings.InternalPipelines["Default"] = new ResiliencePipelineSettings();

        foreach (var (name, pipelineSettings) in settings.Pipelines)
        {
            var capturedSettings = pipelineSettings;
            services.AddResiliencePipeline(name, builder => ConfigureBuilder(builder, capturedSettings));
        }

        services.AddSingleton<IResilientExecutor, ResilientExecutor>();
        return services;
    }

    internal static void ConfigureBuilder(ResiliencePipelineBuilder builder, ResiliencePipelineSettings settings)
    {
        if (settings.RetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = settings.RetryCount,
                Delay = settings.RetryBaseDelay,
                BackoffType = settings.UseExponentialBackoff
                    ? DelayBackoffType.Exponential
                    : DelayBackoffType.Constant,
                UseJitter = settings.UseJitter,
            });
        }

        if (settings.EnableCircuitBreaker)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = settings.CircuitBreakerFailureRatio,
                MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                SamplingDuration = settings.CircuitBreakerSamplingDuration,
                BreakDuration = settings.CircuitBreakerBreakDuration,
            });
        }

        if (settings.EnableTimeout)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = settings.TimeoutPerAttempt,
            });
        }
    }
}
