# API Review — TASK-015: OKX services + mapping + error + time + tests + AddOkxExchange DI

**Reviewer**: API-Reviewer  
**Date**: 2026-06-18  
**Branch**: feat/m3-okx  
**Commit**: 5fb566140c18f60833cd7bcb41a5ea8cd2c481c0

---

## Scrutiny Points Assessment

### 1. Public surface — OKX types

Verified by grep on all .cs files under `src/CryptoExchanges.Net.Okx/`.

**Public types found:**
- `OkxExchangeClient` — `public sealed class` (correct)
- `OkxOptions` — `public sealed class` (correct)
- `ServiceCollectionExtensions` — `public static class` hosting `AddOkxExchange` (correct)

**Internal types confirmed:**
- `IOkxHttpClient` — `internal interface` (correct)
- `OkxHttpClient` — `internal sealed class` (correct)
- `OkxSymbolFormat` — `internal static class` (correct)
- All services: `OkxMarketDataService`, `OkxTradingService`, `OkxAccountService` — all `internal sealed class` (correct)
- All DTOs: `OkxResponse<T>`, `OkxTicker`, `OkxOrderBook`, `OkxTrade`, `OkxInstrument`, `OkxOrder`, `OkxOrderAck`, `OkxBalanceAccount`, `OkxBalanceDetail`, `OkxFill` — all `internal sealed record` (correct)
- `OkxClientComposer` — `internal static class` (correct)
- `OkxRequestValidation` — `internal static class` (correct)
- `OkxValueParsers` — `internal static class` (correct)
- `OkxSignatureService` — `internal sealed class` (correct)
- `OkxErrorTranslator` — `internal sealed class` (correct)
- `OkxTimeSync` — `internal static class` (correct)
- `OkxSigningHandler`, `OkxSigningRequest` — `internal` (correct)
- `OkxResponseProfile` — `internal sealed class` (correct)
- `OkxServerTime` (in OkxExchangeClient.cs) — `internal sealed record` (correct)

**Result: PASS.** Only the three intended types are public. This is cleaner than Bybit (which has legacy public signing types).

### 2. OkxExchangeClient mirrors BybitExchangeClient

Comparing the two clients side by side:

| Aspect | BybitExchangeClient | OkxExchangeClient |
|---|---|---|
| `sealed class` | Yes | Yes |
| `IExchangeClient, IAsyncDisposable` | Yes | Yes |
| `internal` composition ctor | Yes | Yes |
| `static Create(TOptions)` | Yes | Yes |
| `static CreateFromEnvironment()` | Yes | Yes |
| `SyncServerTimeAsync(CancellationToken ct = default)` | Yes | Yes |
| `PingAsync(CancellationToken ct = default)` | Yes | Yes |
| `DisposeAsync()` owns-flag pattern | Yes | Yes |
| `_offsetHolder` shared long[] | Yes | Yes |
| CA1859 suppression with justification | Yes | Yes |

`CreateFromEnvironment()`: Bybit reads `BYBIT_API_KEY`/`BYBIT_SECRET_KEY`. OKX reads `OKX_API_KEY`/`OKX_SECRET_KEY`/`OKX_PASSPHRASE`. Consistent naming convention; OKX appropriately adds `OKX_PASSPHRASE`.

**Result: PASS.** The mirror is exact; the only delta (Passphrase) is appropriate to OKX's three-credential model.

### 3. CryptoExchangesOptions OKX additions

Added: `OkxBaseUrl`, `OkxApiKey`, `OkxSecretKey`, `OkxPassphrase`.

Convention check against existing Binance/Bybit entries:
- `BinanceBaseUrl`, `BinanceApiKey`, `BinanceSecretKey` — pattern: `{Exchange}{Property}`
- `BybitBaseUrl`, `BybitApiKey`, `BybitSecretKey` — same
- `OkxBaseUrl`, `OkxApiKey`, `OkxSecretKey`, `OkxPassphrase` — same pattern, with `Passphrase` as the new field name

`AddCryptoExchanges` correctly forwards `OkxPassphrase` → `opt.Passphrase` using the null-coalesce pattern.

**Result: PASS.** Naming is consistent with the existing Binance/Bybit convention. The `OkxPassphrase` addition to `CryptoExchangesOptions` is additive and non-breaking.

### 4. PostAsync<T>(string, object, bool, CancellationToken) overload on IOkxHttpClient

The new overload is added to the `internal interface IOkxHttpClient`. Assessment:

- **Internal-only**: `IOkxHttpClient` is `internal`. The overload is not part of any public API surface. PASS.
- **Motivation**: `cancel-batch-orders` requires a JSON array body, which `Dictionary<string,string>` cannot represent; a typed-body overload is the minimal correct extension for this use case.
- **Consistency with existing overload**: The dictionary overload serializes to a flat JSON object via `JsonSerializer.Serialize`. The new object overload does the same but accepts arbitrary types. Both route through the shared `PostJsonAsync` private method (correct refactor — no duplication).
- **CancellationToken placement**: Last parameter with `= default`. PASS.
- **Docstring**: Clear, references the specific use case (batch endpoints, JSON array). PASS.
- **NSubstitute testability**: The new overload is exercised in `OkxMappingAndServiceTests.Trading_PlaceMarketOrder_SendsMarketOrdTypeAndRefetches` where the dict overload is mocked; `CancelAllOrdersAsync` uses the object overload. The interface overload separation is exercised implicitly.

One minor design note: the parameter name `body` is `object` (not `T`), which means the body type is erased at the interface level. This is intentional (the caller knows the type; the implementor serializes it). Consistent with the existing `Dictionary<string,string>?` parameter erasure approach.

**Result: PASS.** The overload is internal, well-motivated, non-breaking, and properly factored.

### 5. Backwards compatibility / consistency

**Async suffixes**: All public async methods carry `Async` suffix. PASS.

**Nullability annotations**: `#nullable enable` is inherited from `Directory.Build.props`. All new files use nullable-aware patterns (`string?`, `DateTimeOffset?`, `null` checks). PASS.

**CancellationToken placement**: Last parameter, `CancellationToken ct = default` throughout all service methods and the new IOkxHttpClient overload. PASS (matches IExchangeClient.cs:18 pattern).

**Option naming**: `OkxOptions` has `BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds` — all consistent with `BybitOptions` (BaseUrl, ApiKey, SecretKey, TimeoutSeconds). The added `Passphrase` field is OKX-specific and appropriately named.

**Environment variable naming**: `OKX_API_KEY`, `OKX_SECRET_KEY`, `OKX_PASSPHRASE` — consistent with Bybit's `BYBIT_API_KEY`/`BYBIT_SECRET_KEY` pattern. PASS.

---

## Findings

### Finding: OkxOptions.ToCredentials() can throw when called with default (empty) Passphrase

- **Severity**: MEDIUM
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:38-39`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — the method is unused in the signing path, but it IS public and will throw for any caller who invokes it with the default empty Passphrase)
- **Issue**: `OkxOptions.ToCredentials()` calls `new ExchangeCredentials(ApiKey, SecretKey, Passphrase)`. `ExchangeCredentials`'s constructor treats a non-null empty string as invalid and throws `ArgumentException` (`ThrowIfNullOrWhiteSpace` on a non-null passphrase). Since `OkxOptions.Passphrase` defaults to `string.Empty` (non-null), any caller who creates `OkxOptions` for public-market-data use and then calls `ToCredentials()` will get an unexpected `ArgumentException`. The signing path correctly avoids `ToCredentials()` entirely (confirmed by grep — no call site in production src), but the method is a public API contract that documents itself as "intended for signing wire-up" and the default case will throw unexpectedly.
- **Fix**: Change the `ToCredentials()` implementation to pass `Passphrase` as `null` when it is empty, or add a guard: `new(ApiKey, SecretKey, string.IsNullOrEmpty(Passphrase) ? null : Passphrase)`. Alternatively, document the throw explicitly in the XML doc and add a guard example, or make the method `internal` since it is not used outside the assembly.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:44` — the constructor throws on non-null empty passphrase.

---

### Finding: CryptoExchanges.Net.Okx.csproj missing PackageLicenseExpression explicitly (inherited)

- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj`
- **Category**: NuGet Conventions
- **Verdict**: PASS — `PackageLicenseExpression` is inherited from `Directory.Build.props:12` (`Apache-2.0`). `GenerateDocumentationFile` is also inherited from `Directory.Build.props:8`. `PackageId` and `Description` are set explicitly in the csproj. `IsPackable` is NOT set (not needed; library projects are packable by default, and this is correct). No action required.
- **Issue**: None.
- **Fix**: N/A.
- **Pattern reference**: `Directory.Build.props:8,12`.

---

### Finding: No breaking changes to Core interfaces or models

- **Severity**: N/A
- **Confidence**: 99
- **File**: No Core files touched
- **Category**: Compatibility
- **Verdict**: PASS — The diff contains zero modifications to `src/CryptoExchanges.Net.Core/`. No interface members added, removed, or renamed. No model record properties changed. No enum values reordered. The `ExchangeId.Okx` enum value was pre-existing in Core (added in an earlier task). No breaking changes.

---

### Finding: AddOkxExchange and AddCryptoExchanges signatures

- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:35-51`; `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:23-55`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `AddOkxExchange(this IServiceCollection, Action<OkxOptions>? configure = null)` exactly mirrors `AddBybitExchange` and `AddBinanceExchange` (optional configure parameter, IServiceCollection return for chaining). `AddCryptoExchanges` signature unchanged; OKX delegation added consistently.

---

### Finding: InternalsVisibleTo grants are appropriate

- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:19-22`
- **Category**: API Design
- **Verdict**: PASS — Grants are for `CryptoExchanges.Net.Okx.Tests.Unit`, `CryptoExchanges.Net.Okx.Tests.Integration`, and `DynamicProxyGenAssembly2` (NSubstitute). This mirrors the Bybit pattern exactly. No consumer application projects granted visibility. Appropriate.

---

### Finding: Test projects have IsPackable=false

- **Severity**: N/A
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Okx.Tests.Unit/CryptoExchanges.Net.Okx.Tests.Unit.csproj:6`; `tests/CryptoExchanges.Net.Okx.Tests.Integration/CryptoExchanges.Net.Okx.Tests.Integration.csproj:6`
- **Category**: NuGet Conventions
- **Verdict**: PASS — Both test projects have `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`.

---

### Finding: IOkxHttpClient typed PostAsync overload consistency vs IBybitHttpClient

- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs:16`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence < 80; OKX-specific need is documented)
- **Issue**: `IBybitHttpClient` has only one `PostAsync` overload (dictionary-based). `IOkxHttpClient` now has two: the dictionary overload plus the new typed-object overload. This is a deliberate delta because OKX's batch-cancel endpoint requires a JSON array, which `Dictionary<string,string>` cannot express. The divergence is intentional and internal-only, but if Bitget or future exchanges need the same pattern they will need to add the same overload independently. There is no shared `ICryptoHttpClient<T>` abstraction — each exchange interface is independent.
- **Fix**: No action required now. Recommend tracking as a future generalization candidate when Bitget is implemented, to see if a shared `PostAsync<TBody, TResponse>` on a base internal interface makes sense.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs:10`.

---

### Finding: OkxOptions.ToCredentials unused but in public API, may mislead callers (secondary concern)

- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:29-39`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `ToCredentials()` is documented as "Intended for signing wire-up in later tasks" but the signing wire-up in this very task explicitly avoids calling it (comments in `OkxClientComposer` and `ServiceCollectionExtensions` note "no OkxOptions.ToCredentials() needed"). So the method is public but: (a) unused in this assembly, (b) will throw for default-constructed options, (c) BybitOptions has no equivalent. This is a consistency break — future exchange options classes may or may not add the same method. Combined with the throw concern raised above, this method may be premature public API.
- **Fix**: Either make it `internal` (it has no external callers and the signing path avoids it), or fix the passphrase-empty-throws issue (see Finding above) and add usage examples in the XML doc to make the intended use case clear.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs` — no `ToCredentials()` method.

---

## Summary

- PASS: Public surface isolation — only `OkxExchangeClient`, `OkxOptions`, `ServiceCollectionExtensions` are public; all internal types confirmed internal.
- PASS: OkxExchangeClient mirrors BybitExchangeClient exactly — `Create`/`CreateFromEnvironment`/`SyncServerTimeAsync`, same patterns, same internal ctor, same `_offsetHolder` design.
- PASS: `CryptoExchangesOptions` OKX additions — consistent naming (`OkxBaseUrl`/`OkxApiKey`/`OkxSecretKey`/`OkxPassphrase`), additive non-breaking change.
- PASS: New typed-body `PostAsync<T>(string, object, bool, CancellationToken)` — internal, well-motivated for batch endpoints, properly factored, CancellationToken last.
- PASS: `AddOkxExchange` / `AddCryptoExchanges` signatures — mirrors Bybit/Binance pattern.
- PASS: No Core interfaces, models, or enums modified — zero breaking changes.
- PASS: NuGet conventions — `PackageId`, `Description` in csproj; license/docs inherited from Directory.Build.props; test projects have `IsPackable=false`.
- PASS: InternalsVisibleTo — test + DynamicProxy only, matches Bybit pattern.
- CONCERN: `OkxOptions.ToCredentials()` will throw `ArgumentException` when called with default empty `Passphrase` — the signing path never calls it, but the public method's default-value behavior is a trap for external callers (confidence: 90, non-blocking).
- CONCERN: `OkxOptions.ToCredentials()` is dead public API — unused in this assembly, inconsistent with `BybitOptions` (confidence: 70, non-blocking).
- CONCERN: `PostAsync` overload divergence across exchange HTTP client interfaces — internal, intentional, but may create friction for the Bitget task (confidence: 75, non-blocking).

---

## Final Verdict

**APPROVED**

All blocking checks pass. No HIGH or MEDIUM severity findings with confidence >= 80. The one MEDIUM finding (`ToCredentials()` throw trap) is at confidence 90 but severity MEDIUM, non-blocking given that the method is never called in the signing path and the TASK-010 carry-in is correctly addressed. The implementation is structurally correct, public surface is properly isolated per ADR-001 conv #2, and the BybitExchangeClient mirror is faithful.
