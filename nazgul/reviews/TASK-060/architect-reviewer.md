---
verdict: APPROVE
---

# Architect Review: TASK-060 — AddKucoinExchange DI + AddCryptoExchanges + MCP wiring

## Summary

All hard architectural constraints pass. The diff is a clean, faithful extension of the OKX/Bitget DI pattern to KuCoin. One pre-existing pattern concern is surfaced (non-blocking).

---

## Findings

### Finding: ADR-001 compliance — AddKucoinExchange in exchange assembly

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `AddKucoinExchange` lives entirely in `CryptoExchanges.Net.Kucoin`, not in the DI aggregator. The aggregator delegates to it without reimplementing any wiring. Consumers who want KuCoin only can reference the Kucoin assembly and call `AddKucoinExchange` directly — they do not transitively pull in Binance, Bybit, OKX, or Bitget. ADR-001 is satisfied.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:32-71`

---

### Finding: 4-layer chain — no forbidden references in Kucoin csproj

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/CryptoExchanges.Net.Kucoin.csproj:11-14`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- The Kucoin assembly references only Core and Http — no DependencyInjection, no Mcp, no other exchange assembly. The DI aggregator and MCP projects add references to Kucoin (not the reverse). Layering invariant holds.

---

### Finding: Keyed singleton parity — ExchangeId.Kucoin

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:148`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- Registration delegates to `ExchangeServiceRegistration.AddExchange<KucoinOptions, IMapper>` with `ExchangeId.Kucoin` as the key. No unkeyed `IExchangeClient` is registered. `AddKucoinExchange_NoUnkeyed_ExchangeClient_Registered` test explicitly validates this.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:123-128`

---

### Finding: ValidateOnStart fail-fast

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:76-78`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `ValidateOnStart()` is called unconditionally inside `ExchangeServiceRegistration.AddExchange`. `TimeoutSeconds > 0` and `BaseUrl` non-empty validations fire at startup. Two tests cover these: `AddKucoinExchange_InvalidOptions_FailFast_TimeoutZero` and `AddKucoinExchange_BaseUrlWithPath_FailFast`.

---

### Finding: No captive dependency — named HttpClient pattern

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:148-177`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- Registration uses `services.AddHttpClient("kucoin", ...)` (named client), not a typed client. The singleton factory resolves via `IHttpClientFactory.CreateClient("kucoin")` — no transient `HttpClient` captured in a singleton. Matches Invariant 9 exactly.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:98-128`

---

### Finding: Signing handler pattern — DelegatingHandler + offsetHolder

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:163-176`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- The `requestFinalizerFactory` follows the exact OKX/Bitget three-credential signing gate: if either `SecretKey` OR `Passphrase` is empty, a `PassThroughHandler` is returned. When both are present, `KucoinSigningHandler` is constructed with the keyed `long[]` offset holder, and the clock-skew read uses `Interlocked.Read(ref holder[0])`. The `KucoinOptions.ToCredentials()` (which throws on empty passphrase) is correctly bypassed in this path. Three tests exercise the partial-credential cases.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:58-71`

---

### Finding: Single composition root — ComposeForDi

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:177`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `exchangeClientFactory` calls `KucoinClientComposer.ComposeForDi`, which is the single DI assembly point for KuCoin. The composer resolves the keyed `ISymbolMapper`, keyed `IMapper`, and `IExchangeTimeSync`, then calls `ComposeWith`. `ownsHttpClient: false` is correctly enforced — the `IHttpClientFactory` owns the client lifetime.
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/Internal/KucoinClientComposer.cs:59-68`

---

### Finding: MCP wiring — explicit ProjectReference

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj:18`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- MCP adds a direct `ProjectReference` to `CryptoExchanges.Net.Kucoin`. No `Program.cs` change is required because `AddCryptoExchanges` is already the MCP entry point — calling it now includes KuCoin.

---

### Finding: Test coverage — scope cleanliness and aggregator

- **Severity**: N/A
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- 10 DI tests cover: keyed resolution, secretless path, partial-credential path, `ValidateOnStart` (two failure modes), mapper singleton semantics, no unkeyed registration, scope-graph validity (ValidateScopes + ValidateOnBuild), `AddCryptoExchanges` Kucoin resolution, all-five-exchanges aggregator resolution, and option delegation from `CryptoExchangesOptions` through `AddCryptoExchanges` to `KucoinOptions`. All 161 tests pass, build succeeds with zero warnings, zero errors.

---

### Finding (CONCERN): CryptoExchangesOptions.cs grows one exchange at a time — latent coupling as exchange count scales

- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs:63-72`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `CryptoExchangesOptions` is a flat class with four properties per exchange (BaseUrl, ApiKey, SecretKey, Passphrase). It now carries 20 properties for 5 exchanges. Each new exchange requires 4 property additions and a corresponding block in `AddCryptoExchanges`. This is not a violation today — it is the established pattern — but the growth is O(N) and the class is in the DI aggregator (a package every consumer of `AddCryptoExchanges` takes). A structural alternative would be a dictionary-or-per-exchange nested options shape, but changing it would be a breaking API change. The pre-existing pattern is sound for a small exchange count; the concern is that it becomes a friction point around exchange #8-10.
- **Fix**: No action required for this task. Raise as a macro-architecture note for the roadmap: consider a per-exchange nested options record (`CryptoExchangesOptions.Kucoin = new KucoinOverrideOptions { ... }`) for N > 6. Change would be breaking; schedule for a major version bump.

---

### Milestone architecture note (FEAT-006 close — KuCoin completes 5-exchange round)

With TASK-060 merged, `CryptoExchanges.Net.DependencyInjection` depends on all five exchange assemblies. The package dependency graph continues to satisfy ADR-001: each exchange is independently consumable (a Kucoin-only consumer takes Kucoin + Http + Core, not Binance/Bybit/OKX/Bitget). The aggregator is an opt-in convenience, not a mandatory transitive dependency.

One duplication observation worth recording: `ApplyEnvDefaults` (read 3 env vars, assign if non-empty) is structurally identical across OKX, Bitget, and KuCoin `ServiceCollectionExtensions.cs` files. It cannot currently be factored to Http without a shared options interface, which is a larger change. If a 6th exchange lands with the same pattern, a small `IHasApiCredentials` interface in Core is worth the investment to eliminate the N-copy boilerplate.

The `CryptoExchangesOptions` concern above is this milestone's primary technical debt signal. It is non-blocking.

---

## Verdict

**APPROVED.** All hard architectural constraints pass. Build is clean. All 161 tests pass. No blocking findings.
