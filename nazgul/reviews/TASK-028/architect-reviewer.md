# Architect Review — TASK-028

## Verdict: APPROVED

## Score: 91/100

## Findings

### NOTE Redundant ProjectReferences in Mcp.csproj (confidence: 95%)
The Mcp csproj explicitly references all four exchange projects (Binance, Bybit, Okx, Bitget) *in addition to* `CryptoExchanges.Net.DependencyInjection`. Since the DI project already transitively pulls in all four exchange assemblies (it references them at compile time — `ServiceCollectionExtensions.cs:1-4`), the four explicit exchange `ProjectReference` nodes in Mcp.csproj are redundant at the MSBuild level. They compile fine and add no bug risk. However, they do encode an implicit assumption that every exchange will always live in the DI aggregator, which conflicts slightly with the "thin facade above DI" intent stated in the task. If a future exchange is added but not yet in the DI package, the Mcp project would silently stop seeing it via its direct references — the design already relies on `AddCryptoExchanges` in Program.cs for the actual registrations. **Non-blocking**: the pattern is internally consistent and the build is clean.

### CONCERN EnvCredentialBinder is a `static class` for behavior the maintainer might swap (confidence: 72%)
Invariant 11 (DIP mandate, 2026-06-18) flags new `static class`es that hold swappable behavior. `EnvCredentialBinder` is a pure, stateless transformation function — no network, no global state — and it takes its `getEnv` delegate as a parameter (making it effectively injectable by the caller). This is much closer to a "fixed pure helper" than a "behavior the maintainer might swap". However, it currently has no interface, which means if a future task needs to support alternative credential sources (e.g. AWS Secrets Manager, Azure Key Vault) it would require adding a new class rather than swapping an implementation. Confidence is below the 80 threshold because the function-parameter injection (`Func<string, string?>`) already provides the seam. Flag for awareness; an `ICredentialBinder` interface could be added by a future task if the use-case materializes. Non-blocking.

### NOTE Mcp test project references both Mcp AND DependencyInjection directly (confidence: 85%)
`CryptoExchanges.Net.Mcp.Tests.Unit.csproj` lists both `CryptoExchanges.Net.Mcp` and `CryptoExchanges.Net.DependencyInjection` as `ProjectReference`. The DI reference is only needed because `CryptoExchangesOptions` comes from that assembly. Since `CryptoExchanges.Net.Mcp` already depends on DI, the DI reference in the test project is a transitive duplicate. It compiles and is not wrong, but it creates a subtle coupling where the test project must be kept in sync with the Mcp project's own DI reference. Non-blocking, informational only.

### PASS Dependency direction (confidence: 99%)
No Core or Http project files were modified. `Mcp.csproj` references only exchange libs + DI — all downstream of Mcp in the consumption graph. The dependency chain Core → Http → Exchange → DI → Mcp is fully intact. No inversions.

### PASS No Core / Http changes (confidence: 100%)
Diff touches only: solution file, plan/task manifests, the two new `src/CryptoExchanges.Net.Mcp/` files, and the test project. Zero modifications under `src/CryptoExchanges.Net.Core/` or `src/CryptoExchanges.Net.Http/`.

### PASS Thin facade intent (confidence: 99%)
Program.cs contains no exchange logic — it delegates entirely to `AddCryptoExchanges` (the aggregator). `WithToolsFromAssembly()` is a placeholder for Tool types that will be added in later tasks (TASK-030, 031, 032). No new exchange behavior or business logic was introduced.

### PASS Host model alignment (confidence: 97%)
`PackAsTool=true`, `ToolCommandName=crypto-mcp`, stdio MCP transport, and stderr-only logging (`LogToStandardErrorThreshold = LogLevel.Trace`) are all correct choices for an LLM-agent-facing local tool. The stdio transport contract requires stdout to carry only MCP protocol frames; routing console logs to stderr protects that contract.

### PASS SLN wiring (confidence: 100%)
Both `CryptoExchanges.Net.Mcp` and `CryptoExchanges.Net.Mcp.Tests.Unit` are correctly added under the `src` and `tests` solution folders respectively (GUIDs map to `{827E0CD3}` for src and `{0AB3BF05}` for tests). All 12 build-configuration rows are present for each project.

### PASS Build clean (confidence: 100%)
`dotnet build` with `TreatWarningsAsErrors=true` produces 0 warnings, 0 errors across all 21 projects.

### PASS CA1515 suppression is justified (confidence: 92%)
`EnvCredentialBinder` must be `public` so the test assembly (a separate project) can reference it. The suppression is correctly scoped to the Mcp project only and the comment in the csproj states the rationale. Pattern is consistent with how other exchange test projects use `InternalsVisibleTo`.

### PASS No new exchange interface modifications (confidence: 100%)
`IMarketDataService`, `ITradingService`, `IAccountService`, and `IExchangeClient` are unchanged.

### PASS No new signing path or retry-impacting code (confidence: 100%)
No HTTP handlers, signing, or resilience pipeline code was added in this scaffold task.

## Summary

TASK-028 delivers a correct, minimal scaffold for the MCP stdio host. The dependency direction is sound — Mcp sits above the DI aggregator without touching Core or Http. Program.cs is appropriately thin. The host model (PackAsTool + stdio transport + stderr-only logging) is the right pattern for an agent-facing tool. Two minor non-blocking notes: (1) the four explicit exchange `ProjectReference` nodes in Mcp.csproj are redundant given the DI project already carries them transitively, and (2) `EnvCredentialBinder` is a `static class` that meets the "fixed pure helper" exception in Invariant 11 due to its delegate-injection seam, but an `ICredentialBinder` interface could be a future improvement if alternative credential sources are needed. The solution builds cleanly with zero warnings.
