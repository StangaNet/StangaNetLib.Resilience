# StangaNetLib.Resilience — Copilot instructions

- This library wraps `Microsoft.Extensions.Resilience` (Polly v8) and returns `Result` / `Result<T>` from `StangaNetLib.Core` instead of throwing for expected resilience failures.
- Prefer `ResilienceErrors` for retry exhaustion, circuit open, timeout, and cancellation; reserve exceptions for invalid arguments.
- Keep public APIs documented (`///`), pipeline names explicit, and free of host-specific infrastructure.
- DI entry point is `AddStangaNetLibResilience` in `StangaNetLib.Resilience.Extensions`.
- Match existing naming, file-scoped namespaces, and EditorConfig / Roslyn conventions.
- Tests use xUnit v3 + FluentAssertions; cover both `net8.0` and `net9.0` when changing shared behavior.
