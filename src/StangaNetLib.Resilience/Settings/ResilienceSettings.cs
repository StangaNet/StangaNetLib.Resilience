namespace StangaNetLib.Resilience.Settings;

/// <summary>Root configuration for StangaNetLib.Resilience services.</summary>
public sealed class ResilienceSettings
{
    /// <summary>The <c>appsettings.json</c> section key. Value: <c>"ResilienceSettings"</c>.</summary>
    public const string SectionName = "ResilienceSettings";

    private Dictionary<string, ResiliencePipelineSettings> _pipelines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Default"] = new ResiliencePipelineSettings()
    };

    /// <summary>
    /// Named pipeline configurations. Each key becomes a pipeline name consumed by
    /// <see cref="StangaNetLib.Resilience.Execution.IResilientExecutor.ExecuteAsync{T}"/>.
    /// A <c>"Default"</c> pipeline with production-safe defaults is always present.
    /// </summary>
    /// <remarks>
    /// Keys are matched case-insensitively. Adding additional keys registers additional Polly
    /// pipelines so different operations (e.g. <c>"Http"</c>, <c>"Database"</c>) can have
    /// independent tuning without sharing circuit-breaker state.
    /// </remarks>
    public IReadOnlyDictionary<string, ResiliencePipelineSettings> Pipelines => _pipelines;

    /// <summary>
    /// Internal setter used by the configuration binder to populate pipelines.
    /// </summary>
    internal Dictionary<string, ResiliencePipelineSettings> InternalPipelines
    {
        set => _pipelines = value ?? new(StringComparer.OrdinalIgnoreCase);
        get => _pipelines;
    }
}
