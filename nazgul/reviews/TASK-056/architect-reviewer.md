---
reviewer: architect-reviewer
task: TASK-056
verdict: APPROVE
confidence: 98
---

# Architect Review — TASK-056

## Summary

TASK-056 delivers the KuCoin exchange scaffold: a new `CryptoExchanges.Net.Kucoin` library project plus two test projects wired into the solution. All architectural invariants for a scaffold task are met. No blocking violations were found.

## Findings

### Dependency chain is correct (SEVERITY: LOW | confidence: 99%)

`CryptoExchanges.Net.Kucoin.csproj` references only `CryptoExchanges.Net.Core` and `CryptoExchanges.Net.Http` — no reference to Binance, OKX, Bybit, Bitget, or the DI aggregator. This is the correct and expected position in the 4-layer chain. Pattern reference: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:11-14`.

**Verdict: PASS**

### InternalsVisibleTo declared for all three required targets (SEVERITY: LOW | confidence: 99%)

The csproj declares `InternalsVisibleTo` for `CryptoExchanges.Net.Kucoin.Tests.Unit`, `CryptoExchanges.Net.Kucoin.Tests.Integration`, and `DynamicProxyGenAssembly2`. This matches the OKX and Binance reference pattern exactly. Pattern reference: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:17-22`.

**Verdict: PASS**

### KucoinOptions is correctly public; KucoinSymbolFormat is correctly internal (SEVERITY: LOW | confidence: 99%)

`KucoinOptions` is `public sealed` — intentional, it is the configuration surface exposed to consumers. `KucoinSymbolFormat` is `internal static` — correct, it is a wire-format constant holder not part of the public API. The `static` usage here is acceptable under Invariant 11 because `KucoinSymbolFormat` holds a single fixed `SymbolFormat` value object; it is not "swappable behavior" — it encodes a hard fact about KuCoin's wire protocol. This matches the identical `OkxSymbolFormat` pattern. Pattern reference: `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs`.

**Verdict: PASS**

### Unit test project references CryptoExchanges.Net.DependencyInjection (SEVERITY: LOW | confidence: 95%)

`CryptoExchanges.Net.Kucoin.Tests.Unit.csproj` includes a `ProjectReference` to `CryptoExchanges.Net.DependencyInjection`. This is identical to the OKX and Binance unit test pattern (`tests/CryptoExchanges.Net.Okx.Tests.Unit` and `tests/CryptoExchanges.Net.Binance.Tests.Unit` have the same reference). The reference is in a test-only project (`IsTestProject=true`, `IsPackable=false`) and is never shipped as a NuGet transitive dependency to consumers. At this scaffold stage the DI aggregator does NOT yet reference KuCoin (that wiring is a future task), so no circular dependency exists. There is a latent pattern concern here (noted below) but it is non-blocking for this task.

**Verdict: PASS**

### Hard build constraints satisfied via Directory.Build.props (SEVERITY: LOW | confidence: 99%)

`TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`, `GenerateDocumentationFile=true`, and `TargetFramework=net10.0` are inherited from the repo-root `Directory.Build.props` and apply to all projects in the solution including the new ones. No per-project override is needed. Confirmed: `Directory.Build.props:3-8`.

**Verdict: PASS**

### One type per file convention respected (SEVERITY: LOW | confidence: 99%)

- `KucoinOptions.cs` — one public type
- `KucoinSymbolFormat.cs` — one internal type
- `GlobalUsings.cs` — global using directives file (not a type, convention-file)
- `KucoinIntegrationPlaceholder.cs` — one internal type (anchor)
- `ScaffoldSmokeTests.cs` — one public test class

**Verdict: PASS**

### XML documentation on public API (SEVERITY: LOW | confidence: 98%)

`KucoinOptions` carries full `<summary>` XML docs on the class and on all public properties and the `ToCredentials()` method, including `<returns>` and `<exception>`. `GenerateDocumentationFile=true` is active. `KucoinSymbolFormat` being internal does not require XML docs, but the class `<summary>` and the `Instance` member `<summary>` are present. `CS1591` is suppressed in the NoWarn list, consistent with every other exchange project.

**Verdict: PASS**

### Scaffold smoke tests are appropriate and access internal member correctly (SEVERITY: LOW | confidence: 98%)

`ScaffoldSmokeTests` tests `KucoinSymbolFormat.Instance` directly. This compiles because the unit test assembly is listed in `InternalsVisibleTo`. The test count (7 facts) is appropriate for a scaffold: each default property of `KucoinOptions` and both shape properties of `KucoinSymbolFormat.Instance` are covered. There are no tests for `ToCredentials()` at this stage, which is acceptable — the credential constructor validation belongs to Core and is tested there; the scaffold tests prove instantiation and defaults only.

**Verdict: PASS**

### CONCERN: Unit test DI reference is a pattern-level coupling smell (non-blocking) (confidence: 72%)

All per-exchange unit test projects (Binance, OKX, KuCoin) reference `CryptoExchanges.Net.DependencyInjection`. This is a test-time-only reference and does not affect consumer NuGet graphs. However, as the exchange count grows (Binance, Bybit, OKX, Bitget, KuCoin, ...), each new exchange unit test project pulls in the full DI aggregator, which in turn pulls in ALL other exchange assemblies transitively into the test build. This means adding exchange N forces CI to rebuild N-1 unrelated exchange assemblies when running any single exchange's unit tests. The pattern is not wrong today (5 exchanges), but may become materially noisy at 8–10 exchanges. A future option is to test DI composition only in `CryptoExchanges.Net.DependencyInjection.Tests.Unit` and drop the aggregator reference from individual exchange unit test projects. No action required in TASK-056; flagging for the milestone architecture pass.

**Verdict: CONCERN (non-blocking)**

## Verdict: APPROVE

The scaffold delivers exactly what TASK-056 requires: a structurally correct KuCoin project slot with the right dependency chain, InternalsVisibleTo declarations, public `KucoinOptions`, internal `KucoinSymbolFormat`, properly configured test projects, and passing smoke tests. All architectural invariants (1, 2, 3, 10, 11) are satisfied. The one concern raised (unit test DI aggregator reference) is a pre-existing pattern shared with OKX and Binance and is non-blocking for this task.
