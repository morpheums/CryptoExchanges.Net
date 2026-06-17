# Style Conventions

## Naming Conventions
- Variables/fields: `camelCase` with `_` prefix for private instance fields — `SymbolMapper.cs:16-22` (`_format`, `_canonicalToWire`, `_wireToSymbol`)
- Properties: `PascalCase` — `Models.cs:12` (`Base`, `Quote`), `BinanceOptions.cs:16` (`BaseUrl`, `ApiKey`)
- Methods: `PascalCase` — `SymbolMapper.cs:43` (`ToWire`), `BinanceExchangeClient.cs:101` (`SyncServerTimeAsync`)
- Async methods: `PascalCase` + `Async` suffix — `BinanceHttpClient.cs:26` (`GetAsync`), all service methods
- Interfaces: `I` prefix + `PascalCase` — `IExchangeClient.cs:152` (`IExchangeClient`, `IMarketDataService`)
- Generic type parameters: single `T` or descriptive `TPoco` — `BinanceHttpClient.cs:26`
- Constants/static readonlys: `PascalCase` — `Asset.cs:32` (`Btc`, `Eth`, `Usdt`), `ServiceCollectionExtensions.cs:25` (`ClientName`)
- Private constants: `PascalCase` — `ServiceCollectionExtensions.cs:25` (`private const string ClientName`)

## File Naming
- Files named after primary type: `PascalCase.cs` — `BinanceExchangeClient.cs`, `SymbolMapper.cs`, `ErrorTranslationHandler.cs`
- Exception: grouping files for related small types — `Models.cs`, `Enums.cs` (multiple related types in one file)
- Test files named after tested class + `Tests` suffix — `CoreTests.cs`, `BinancePipelineEndToEndTests.cs`

## Directory Structure
- Pattern: **Layer-based within each project**, **feature-based across projects**
- Evidence:
  - Each project = one layer (Core, Http, Binance, DI)
  - Within Binance: `Auth/`, `Internal/`, `Mapping/`, `Resilience/`, `Services/` subdirectories by concern
  - Within Core: `Enums/`, `Exceptions/`, `Interfaces/`, `Models/`, `Resilience/` subdirectories by concern

## Import Style
- Global usings enabled project-wide: `Directory.Build.props:4` (`<ImplicitUsings>enable</ImplicitUsings>`)
- Explicit usings for non-implicit namespaces at top of each file: `BinanceHttpClient.cs:1-6`
- Relative namespaces (no path aliases): `using CryptoExchanges.Net.Core.Interfaces;`
- No barrel files / re-export index files
- `GlobalUsings.cs` in Binance project for project-wide using directives: `src/CryptoExchanges.Net.Binance/GlobalUsings.cs`

## Error Handling
- Pattern: throw immediately at guard points; propagate typed exceptions upward; no swallowing
- Evidence:
  - Guard pattern: `ArgumentNullException.ThrowIfNull(...)` at start of every public/internal method — `SymbolMapper.cs:27`, `ErrorTranslationHandler.cs:24`
  - Typed exception hierarchy surfaced to callers — `ExchangeExceptions.cs:1-93`
  - `OperationCanceledException` re-thrown when `CancellationToken.IsCancellationRequested` — `BinanceExchangeClient.cs:119-122`, `TransientExhaustionHandler.cs:74-76`
  - Exception propagation: `ErrorTranslationHandler` throws typed exception, callers handle at the application boundary
  - `CA1031` (catch all) suppressed in implementation projects with justification — `CryptoExchanges.Net.Binance.csproj:8`

## Logging
- Logger: `Microsoft.Extensions.Logging.Abstractions` declared as a dependency but no active logging calls detected in current source
- No `ILogger<T>` injection into services observed — logging is not yet implemented in services
- Evidence of intent: `CryptoExchanges.Net.Core.csproj:12` (declared dependency)

## Comments
- Style: XML documentation comments (`///`) on ALL public types and members — comprehensive
- Evidence: `SymbolMapper.cs:7-12` (class-level `<summary>` with cross-refs), `IExchangeClient.cs:10-14` (interface member docs), `Asset.cs:10-14` (struct-level docs)
- Inline comments for non-obvious decisions — `BinanceExchangeClient.cs:38-40` (CA1859 suppression justification), `ServiceCollectionExtensions.cs:52-57` (clock-skew holder rationale)
- `#pragma warning disable/restore` always paired with justification comment — `SymbolMapper.cs:46-48`
- No TODOs in committed source (design notes via README roadmap section)
- `CA` suppression NoWarn list with comment explanations in every csproj

## Linter/Formatter
- .NET Roslyn analyzers (AnalysisLevel=latest-all): `Directory.Build.props:6` (`<AnalysisLevel>latest-all</AnalysisLevel>`)
- TreatWarningsAsErrors=true: `Directory.Build.props:7` — all analyzer warnings are compile errors
- `<Nullable>enable</Nullable>`: `Directory.Build.props:5` — full nullable reference type analysis
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>`: `Directory.Build.props:8` — missing XML doc = build error (CS1591)
- Selected CA rules suppressed per-project with justifications — selective, not global suppression
- No `.editorconfig`, no Prettier, no Ruff (pure MSBuild/Roslyn toolchain)

## Git Conventions
- Commit style: conventional-like prefix + imperative description: `feat:`, `fix:`, `test:`, `chore:`, `refactor:`, `polish:`, `harden:`
- Evidence (recent commits):
  - `feat: wire Binance onto resilience pipeline`
  - `fix: add api-key header for key-only clients`
  - `test: e2e re-sign-on-retry through the resilience pipeline`
  - `chore: drop unused using in BinanceExchangeClient`
  - `refactor: extract BinanceClientComposer`
  - `harden: defensively copy SymbolFormat collections in SymbolMapper`
  - `polish: cache factory Available, NotNullWhen on TryGet, fix README DI sample`
- Branch naming: `feat/[milestone-id]-[description]` — e.g. `feat/m1b-cutover`, `feat/di-composition`, `feat/m1-resilience`
- Merge commits used for PRs (not squash or rebase-merge)
