# StangaNetLib.Resilience

[![.NET CI](https://github.com/StangaNet/StangaNetLib.Resilience/actions/workflows/main.yml/badge.svg)](https://github.com/StangaNet/StangaNetLib.Resilience/actions/workflows/main.yml)
![NuGet](https://img.shields.io/badge/nuget-1.0.1-blue)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)

Cross-cutting resilience library that wraps `Microsoft.Extensions.Resilience` (Polly v8) to execute operations through named pipelines. Failures (retry exhaustion, circuit open, timeout, cancellation, unexpected exceptions) become `Result` / `Result<T>` from [`StangaNetLib.Core`](https://github.com/StangaNet/StangaNetLib.Core).

## Design Philosophy

- **Functional failures**: expected resilience outcomes map to `ResilienceErrors`, not exceptions.
- **Domain-centric**: organized by purpose (`Execution`, `Settings`, `Errors`).
- **Validated options**: `ResilienceSettingsValidator` fails fast on invalid pipeline config.
- **Modern .NET**: `net8.0` / `net9.0` / `net10.0`, nullable reference types, documented public API.

## Installation

The package is hosted on **GitHub Packages** (pulls `StangaNetLib.Core` from the same feed).

```xml
<!-- NuGet.config — source key must match your packageSourceCredentials -->
<configuration>
  <packageSources>
    <add key="StangaNet" value="https://nuget.pkg.github.com/StangaNet/index.json" />
  </packageSources>
</configuration>
```

```xml
<!-- .csproj -->
<PackageReference Include="StangaNetLib.Resilience" Version="1.0.0" />
```

See [CHANGELOG.md](./CHANGELOG.md) for release history.

## Core Components

| Namespace | Components | Purpose |
|---|---|---|
| `StangaNetLib.Resilience.Execution` | `IResilientExecutor`, `ResilientExecutor` | Execute operations via named pipelines |
| `StangaNetLib.Resilience.Errors` | `ResilienceErrors` | Typed `Error` factories |
| `StangaNetLib.Resilience.Settings` | `ResilienceSettings`, `ResiliencePipelineSettings`, `ResilienceSettingsValidator` | Options + validation |
| `StangaNetLib.Resilience.Extensions` | `AddStangaNetLibResilience` | DI registration |

## Project Structure

```
StangaNetLib.Resilience/
├── assets/
│   └── package-icon.png
├── build/
│   └── StangaNetLib.Resilience.props
├── src/
│   └── StangaNetLib.Resilience/
│       ├── Errors/
│       ├── Execution/
│       ├── Extensions/
│       └── Settings/
└── tests/
    └── StangaNetLib.Resilience.Tests/
```

## Quick Start

```csharp
builder.Services.AddStangaNetLibResilience(builder.Configuration);
```

```csharp
public class OrderService(IResilientExecutor executor)
{
    public Task<Result<Order>> GetAsync(string id, CancellationToken ct) =>
        executor.ExecuteAsync(
            pipelineName: "Default",
            operation: token => _client.GetOrderAsync(id, token),
            cancellationToken: ct);
}
```

## CI/CD

On push/PR to `main` and `workflow_dispatch`, the shared StangaNet workflow:

1. Restores, builds, and tests on .NET 8 and .NET 9
2. Packs the NuGet package (with symbols)
3. Publishes to GitHub Packages outside pull requests
4. Uploads the `.nupkg` as a build artifact
