# Code Review: TASK-009B — Per-exchange DI re-homing (ADR-001)

**Commit**: 1a56835
**Branch**: feat/m3-okx
**Reviewer**: Code Reviewer Agent
**Date**: 2026-06-18

---

## Verdict: APPROVED
**Confidence**: 97/100

All checks pass. Zero blocking findings. One low-severity concern (dead project references in DI csproj — harmless, non-blocking).

---

## Checklist Results

### 1. Behavioral Drift (AddBinance/AddBybitExchange move — verbatim check)

Compared `4e5ba3f:src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` (original) against the two new per-exchange files.

**Binance** (`src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs`):
- `TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>` — preserved, same `TryAdd` semantics.
- `AddOptions<BinanceOptions>().Configure(ApplyEnvDefaults).Configure(o => configure?.Invoke(o)).Validate(x2).ValidateOnStart()` — chain order identical.
- `TryAddSingleton(sp => ... IOptions<BinanceOptions>().Value)` — preserved.
- `#pragma disable CA1861 / TryAddKeyedSingleton(ExchangeId.Binance, (_, _) => new long[] { 0L }) / restore` — identical, justification comment retained.
- `TryAddKeyedSingleton<ISymbolMapper>`, `TryAddKeyedSingleton<IMapper>` — identical, keyed on `ExchangeId.Binance`.
- `AddHttpClient(ClientName, ...)` configuration block (BaseAddress, Timeout, User-Agent, X-MBX-APIKEY header guard) — identical.
- `ConfigurePrimaryHttpMessageHandler` — identical.
- `ApplyResiliencePipeline` call (all 4 named args, including `usageHeaderName`, factory lambdas) — identical.
- `AddKeyedSingleton<IExchangeClient>(ExchangeId.Binance, ...)` factory lambda — identical.
- `private static void ApplyEnvDefaults(BinanceOptions)` — identical.
- `ClientName = "binance"` const — preserved (was same in original).

**No behavioral drift in Binance.**

**Bybit** (`src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs`):
- Same structural comparison performed. All registration order, lifetimes, `TryAdd` vs `Add` usage, option validation chain, resilience pipeline args, and `BybitClientName = "bybit"` const are identical to the original.

**No behavioral drift in Bybit.**

**DI aggregator** (`src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`):
- Reduced to `AddCryptoExchanges` + `CryptoExchangesOptions`. Content identical to the original `AddCryptoExchanges` body.

**No behavioral drift.**

---

### 2. BinanceHttpClient Endpoint Guards

All four methods guarded as `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` as the **first statement**:

- `GetAsync<T>` — line 30: guard present, first statement. PASS.
- `GetStringAsync` — line 42: guard present, first statement. PASS.
- `PostAsync<T>` — line 54: guard present, first statement. PASS.
- `DeleteAsync<T>` — line 68: guard present, first statement. PASS.

`IBinanceHttpClient` has exactly 4 methods matching those above — full coverage.

Guard placement mirrors the Bybit fix (3 methods in `IBybitHttpClient` / `BybitHttpClient`, all guarded). No asymmetry.

---

### 3. Test Coverage

**New test** `DiRegistrationTests.BybitOnly_Registration_ResolvesBybitClient` (lines 48–61):

- Calls `AddBybitExchange` directly — no aggregator, no Binance registration. Meaningful isolation.
- `await using var sp = services.BuildServiceProvider()` — correct; `ServiceProvider` implements `IAsyncDisposable` in .NET; the resolved `IExchangeClient` is `IAsyncDisposable`-only.
- `GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit).ExchangeId.Should().Be(ExchangeId.Bybit)` — asserts the keyed resolution resolves and returns the right exchange.
- `GetService<IExchangeClient>().Should().BeNull()` — asserts no unkeyed registration. Correct.

Both `using` additions (`CryptoExchanges.Net.Binance` in `DiRegistrationTests.cs`, `CryptoExchanges.Net.Binance` in `ExchangeClientFactoryTests.cs`) are correct: needed because `AddBinanceExchange` is now in those namespaces.

DI unit tests: 11 total (was 10, +1 new). All pass.

---

### 4. ExchangeClientFactory Relocation

- Moved from `CryptoExchanges.Net.DependencyInjection` to `CryptoExchanges.Net.Http`. Namespace updated to `CryptoExchanges.Net.Http`. Accessibility stays `internal`.
- Content is otherwise identical to original (summary doc updated to reference ADR-001 — not behavioral).
- `Lazy<IReadOnlyCollection<ExchangeId>> _available` uses default `LazyThreadSafetyMode.ExecutionAndPublication` — thread-safe, correct for a singleton context.
- `Http.csproj` adds `InternalsVisibleTo` for both Binance and Bybit — required for `new ExchangeClientFactory()` to compile in-assembly registration. Correct.

---

### 5. Binance Signing Types Internalization

- `BinanceSignatureService`: `public sealed class` → `internal sealed class`. Confirmed.
- `BinanceSigningHandler`: `public sealed class` → `internal sealed class`. Confirmed.
- `BinanceSigningRequest`: `public static class` → `internal static class`. Confirmed.
- `AddBinanceExchange` is now in-assembly (`CryptoExchanges.Net.Binance`) so it accesses these types directly without needing the old `InternalsVisibleTo("CryptoExchanges.Net.DependencyInjection")`.
- `CryptoExchanges.Net.Binance.Tests.Integration` retains its `InternalsVisibleTo` (it constructs signing types directly). Correct.

---

### 6. InternalsVisibleTo Cleanup

- `InternalsVisibleTo("CryptoExchanges.Net.DependencyInjection")` removed from both Binance and Bybit csprojs — verified in diff and current csproj files. Build passes = confirms no live reference from DI to exchange internals.

---

### 7. Async / Nullable / General C# 13 Quality

- No new `async` methods introduced (guards in `BinanceHttpClient` are synchronous `ArgumentException` calls, not async).
- All pre-existing `.ConfigureAwait(false)` and `CancellationToken` forwarding in `BinanceHttpClient` unchanged.
- `#pragma warning disable CA1861` in both new `ServiceCollectionExtensions.cs` files: paired with `restore`, justification comment present. Mirrors original pattern.
- XML docs on new `public` types and methods: both `ServiceCollectionExtensions` classes have class-level `<summary>`, `AddBinanceExchange`/`AddBybitExchange` have `<summary>`, `<param>`, `<returns>`. `ClientName`/`BybitClientName` private constants have `<summary>`. `CryptoExchangesOptions` (unchanged) has full docs. `ExchangeClientFactory` `<summary>` updated with ADR-001 reference. All pass.
- No new `catch` blocks introduced.
- No fire-and-forget tasks introduced.

---

## Findings

### Finding: Dead project references in DI csproj
- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The DI csproj retains `ProjectReference` to both `CryptoExchanges.Net.Core` and `CryptoExchanges.Net.Http`. The only C# file in the package (`ServiceCollectionExtensions.cs`) does not `using` either namespace directly — it only references `CryptoExchanges.Net.Binance`, `.Bybit`, and `Microsoft.Extensions.DependencyInjection`. Core and Http are already transitive via the Binance/Bybit references.
- **Fix**: Remove the `<ProjectReference Include="..\CryptoExchanges.Net.Core\...">` and `<ProjectReference Include="..\CryptoExchanges.Net.Http\...">` lines from the DI csproj. Verify build still passes. (These are compile-time transitive, so the build will continue to pass either way — this is purely about expressing accurate direct dependencies.)
- **Pattern reference**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:1-3` (only `Binance`, `Bybit`, `DI.Abstractions` usings)

---

## Test Run

```
dotnet test --filter 'Category!=Integration'
Passed! — Failed: 0, Passed: 241, Skipped: 0
  Core.Tests.Unit:             93
  Http.Tests.Unit:             12
  DI.Tests.Unit:               11 (+1 new BybitOnly test)
  Bybit.Tests.Unit:            80
  Binance.Tests.Integration:   45
```

```
dotnet build CryptoExchanges.Net.sln
Build succeeded. 0 Warning(s), 0 Error(s)
```

---

## Summary

- PASS: Behavioral drift check (Binance) — body-for-body identical to original; same lifetimes, TryAdd/Add, pipeline order
- PASS: Behavioral drift check (Bybit) — body-for-body identical to original
- PASS: BinanceHttpClient endpoint guards — all 4 interface methods guarded as first statement
- PASS: BybitOnly test — meaningful isolation, correct `await using`, keyed resolution + unkeyed null assertion
- PASS: ExchangeClientFactory relocation — namespace updated, Lazy is thread-safe, IVT wired correctly in Http.csproj
- PASS: Binance signing types internalized — BinanceSignatureService, BinanceSigningHandler, BinanceSigningRequest all `internal`
- PASS: InternalsVisibleTo cleanup — DI IVTs removed from both exchange csprojs, build proves no regressions
- PASS: XML docs — all new public types/members documented
- PASS: Pragma pairs — both CA1861 disable/restore blocks have justification comments
- PASS: Build clean — 0 warnings, 0 errors under TreatWarningsAsErrors
- CONCERN: Dead Core+Http project references in DI csproj — confidence 75, non-blocking; harmless but inaccurate direct-dependency declaration
