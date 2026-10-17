using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StangaNetLib.Resilience.Execution;
using StangaNetLib.Resilience.Extensions;
using StangaNetLib.Resilience.Settings;

namespace StangaNetLib.Resilience.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    // ── AddStangaNetLibResilience() — no-arg ──────────────────────────────────

    [Fact]
    public void AddStangaNetLibResilience_NoArgs_RegistersIResilientExecutor()
    {
        var services = new ServiceCollection();

        services.AddStangaNetLibResilience();
        var provider = services.BuildServiceProvider();

        provider.GetService<IResilientExecutor>().Should().NotBeNull();
    }

    [Fact]
    public async Task AddStangaNetLibResilience_NoArgs_DefaultPipelineExecutesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddStangaNetLibResilience();
        var executor = services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();

        var result = await executor.ExecuteAsync("Default", _ => Task.FromResult("ok"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }

    // ── AddStangaNetLibResilience(Action<ResilienceSettings>) — delegate ──────

    [Fact]
    public void AddStangaNetLibResilience_Delegate_RegistersIResilientExecutor()
    {
        var services = new ServiceCollection();

        services.AddStangaNetLibResilience(opts =>
        {
            opts.InternalPipelines["Custom"] = new ResiliencePipelineSettings
            {
                RetryCount = 1,
                EnableCircuitBreaker = false,
                EnableTimeout = false
            };
        });

        var provider = services.BuildServiceProvider();
        provider.GetService<IResilientExecutor>().Should().NotBeNull();
    }

    [Fact]
    public async Task AddStangaNetLibResilience_Delegate_CustomPipelineWorks()
    {
        var services = new ServiceCollection();
        services.AddStangaNetLibResilience(opts =>
        {
            opts.InternalPipelines["Fast"] = new ResiliencePipelineSettings
            {
                RetryCount = 0,
                EnableCircuitBreaker = false,
                EnableTimeout = false
            };
        });
        var executor = services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();

        var result = await executor.ExecuteAsync("Fast", _ => Task.FromResult(99));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(99);
    }

    // ── AddStangaNetLibResilience(IConfiguration) — config ───────────────────

    [Fact]
    public void AddStangaNetLibResilience_Configuration_RegistersIResilientExecutor()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ResilienceSettings:Pipelines:Default:RetryCount"] = "2",
                ["ResilienceSettings:Pipelines:Default:EnableCircuitBreaker"] = "false",
                ["ResilienceSettings:Pipelines:Default:EnableTimeout"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStangaNetLibResilience(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<IResilientExecutor>().Should().NotBeNull();
    }

    [Fact]
    public async Task AddStangaNetLibResilience_Configuration_PipelineExecutesSuccessfully()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ResilienceSettings:Pipelines:Default:RetryCount"] = "1",
                ["ResilienceSettings:Pipelines:Default:EnableCircuitBreaker"] = "false",
                ["ResilienceSettings:Pipelines:Default:EnableTimeout"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStangaNetLibResilience(config);
        var executor = services.BuildServiceProvider().GetRequiredService<IResilientExecutor>();

        var result = await executor.ExecuteAsync("Default", _ => Task.FromResult("configured"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("configured");
    }

    // ── ConfigureBuilder internal helper ─────────────────────────────────────

    [Fact]
    public void ConfigureBuilder_ZeroRetryCount_DoesNotThrow()
    {
        var builder = new Polly.ResiliencePipelineBuilder();
        var settings = new ResiliencePipelineSettings
        {
            RetryCount = 0,
            EnableCircuitBreaker = false,
            EnableTimeout = false
        };

        var act = () => ServiceCollectionExtensions.ConfigureBuilder(builder, settings);

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureBuilder_AllStrategiesEnabled_DoesNotThrow()
    {
        var builder = new Polly.ResiliencePipelineBuilder();
        var settings = new ResiliencePipelineSettings();

        var act = () => ServiceCollectionExtensions.ConfigureBuilder(builder, settings);

        act.Should().NotThrow();
    }
}
