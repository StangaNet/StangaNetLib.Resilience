# Changelog

All notable changes to **StangaNetLib.Resilience** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-10-17

### Added
- Named resilience pipelines via `Microsoft.Extensions.Resilience` (retry, circuit breaker, timeout).
- `IResilientExecutor` / `ResilientExecutor` returning `Result` / `Result<T>` instead of throwing for expected failures.
- Typed `ResilienceErrors` and options validation (`ResilienceSettingsValidator`).
- DI registration (`AddStangaNetLibResilience`) with configuration or delegate setup.
- Dual targeting `net8.0` / `net9.0`, XML docs, symbols (`snupkg`), package icon, and MSBuild consumer metadata stamp.
- Depends on `StangaNetLib.Core` 1.0.0; Microsoft.Extensions.* pinned to 8.x for dual-targeting.
- CI via shared StangaNet reusable workflow (push / PR / manual; publish outside PRs).

[Unreleased]: https://github.com/StangaNet/StangaNetLib.Resilience/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/StangaNet/StangaNetLib.Resilience/releases/tag/v1.0.0
