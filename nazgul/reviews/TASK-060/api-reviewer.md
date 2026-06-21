---
verdict: APPROVE
---

# API Review — TASK-060: `AddKucoinExchange` DI + `AddCryptoExchanges` + MCP wiring

## Diff reviewed
`nazgul/reviews/TASK-060/diff.patch`

---

## API Surface Assessment

### 1. `AddKucoinExchange` signature
```csharp
public static IServiceCollection AddKucoinExchange(
    this IServiceCollection services,
    Action<KucoinOptions>? configure = null)
```
**Match vs. pattern**: Exact parity with `AddOkxExchange` and `AddBitgetExchange` (`src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:32-34`). Extension method on `IServiceCollection`, optional `Action<TOptions>?` with `= null`, returns `IServiceCollection` for chaining. PASS.

### 2. `CryptoExchangesOptions` additions
Four new nullable `string?` properties: `KucoinBaseUrl`, `KucoinApiKey`, `KucoinSecretKey`, `KucoinPassphrase`. All have XML `<summary>` docs. Naming convention is `{Exchange}{Field}` — identical to `OkxBaseUrl`/`OkxApiKey`/`OkxSecretKey`/`OkxPassphrase` and `BitgetBaseUrl`/`BitgetApiKey`/`BitgetSecretKey`/`BitgetPassphrase`. No `KucoinTimeoutSeconds` is exposed, consistent with all other exchanges in the aggregator. PASS.

### 3. Backwards compatibility of `CryptoExchangesOptions` change
All four additions are nullable `string?` properties with `{ get; set; }` on a `sealed class`. Existing callers using object initializer syntax compile unchanged. No positional constructor on this class. PASS.

### 4. `AddCryptoExchanges` return type and null guard
`AddCryptoExchanges` still returns `IServiceCollection` (chainable). `ArgumentNullException.ThrowIfNull(services)` is present at line 29 of the modified file — this is the aggregator's own null guard before delegating to `AddKucoinExchange`. `AddKucoinExchange` itself delegates entirely to `ExchangeServiceRegistration.AddExchange`, which carries its own `ArgumentNullException.ThrowIfNull(services)` at `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:65`. Null guard coverage is consistent with the OKX/Bitget pattern. PASS.

### 5. `KucoinOptions` public shape
`KucoinOptions` at `src/CryptoExchanges.Net.Kucoin/KucoinOptions.cs` exposes `BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds` — matching the minimum required by the reviewer checklist and consistent with `OkxOptions`. The `ToCredentials()` method (not used in DI path, documented as throwing on empty passphrase) is a value-add, not a subtraction. PASS.

### 6. `services` null guard consistency
`AddKucoinExchange` passes `services` directly to `ExchangeServiceRegistration.AddExchange` which unconditionally calls `ArgumentNullException.ThrowIfNull(services)`. The aggregator also guards its own `services` parameter before any delegation. This is identical to the OKX and Bitget pattern. PASS.

### 7. MCP tool-schema unchanged
No changes to `Program.cs`, no MCP tool names altered, no schema types modified. The KuCoin assembly is registered via `AddCryptoExchanges`, which routes through the existing 12-tool vocabulary's `ExchangeId.Kucoin` key with no schema change required. PASS.

### 8. NuGet / csproj conventions
- `CryptoExchanges.Net.Kucoin.csproj` already has `<PackageId>`, `<Description>`, and inherits `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` from `Directory.Build.props`. PASS.
- `CryptoExchanges.Net.Kucoin.Tests.Unit.csproj` has `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`. PASS.
- MCP csproj (`CryptoExchanges.Net.Mcp.csproj`) has `<IsPackable>true</IsPackable>` and `<PackAsTool>true</PackAsTool>` — unchanged from baseline, no regression. PASS.

### 9. `InternalsVisibleTo` usage
`CryptoExchanges.Net.Kucoin.csproj` grants visibility to `CryptoExchanges.Net.Kucoin.Tests.Unit` and `CryptoExchanges.Net.Kucoin.Tests.Integration` (test assemblies only) plus `DynamicProxyGenAssembly2` (Castle/NSubstitute). No consumer application projects are granted visibility. PASS.

### 10. Missing `using CryptoExchanges.Net.Core;` vs. OKX/Bitget pattern
The OKX and Bitget `ServiceCollectionExtensions.cs` files include `using CryptoExchanges.Net.Core;` to access `SymbolMapper` (which lives in `namespace CryptoExchanges.Net.Core`). KuCoin uses `KucoinSymbolMapper` from `CryptoExchanges.Net.Kucoin.Internal` and does NOT need the shared `SymbolMapper` type. The omission is intentional and correct — there is no unused `using` directive to add, and no missing reference. PASS.

### 11. `CancellationToken` / return type conventions
No new methods on `IMarketDataService`, `ITradingService`, or `IAccountService` are introduced. DI wiring only. Convention check is not applicable. PASS.

---

## Findings

### Finding: Task acceptance criteria says "fail-fast on missing api-key" but no ValidateOnStart test for empty ApiKey
- **Severity**: LOW
- **Confidence**: 60
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs:59-88`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: TASK-060 acceptance criteria states "ValidateOnStart fail-fast on missing api-key". The `ExchangeServiceRegistration.AddExchange` shared helper only validates `TimeoutSeconds > 0` and `BaseUrl` is non-empty (`ExchangeServiceRegistration.cs:76-78`) — there is no validation that `ApiKey` is non-empty. The two fail-fast tests cover `TimeoutSeconds = 0` and `BaseUrl` with a path segment, but not an empty `ApiKey`. There is no `ValidateOnStart` rule for `ApiKey` in the shared helper. The test suite is therefore consistent with the actual runtime behavior. However, the acceptance criteria language is slightly misleading: "fail-fast on missing api-key" appears to be satisfied by design through the `PassThroughHandler` gate (secretless = public market data, not a validation error), not via `OptionsValidationException`. This is the same pattern used by OKX and Bitget, and the security reviewer has already noted the runtime-fail gap as a non-blocking concern. No implementation error — the behavior is consistent across all five exchanges.
- **Fix**: Clarify acceptance criteria language in future tasks (or add an explicit `ApiKey` validation rule to `ExchangeServiceRegistration` in a follow-up). Not blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:76-78`

### Finding: `KucoinDiTests.AddCryptoExchanges_KucoinOptions_AppliesViaAggregator` resolves unkeyed `KucoinOptions`
- **Severity**: LOW
- **Confidence**: 55
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs:165`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: Line 165 calls `sp.GetRequiredService<KucoinOptions>()` (unkeyed). This relies on the `services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<TOptions>>().Value)` line in `ExchangeServiceRegistration` (`ExchangeServiceRegistration.cs:80`) which registers the options as an unkeyed singleton for each exchange. When `AddCryptoExchanges` registers all five exchanges, there will be five unkeyed `TOptions` singletons from five different `TryAddSingleton` calls — but because `TryAdd` only registers if not already present, only the first exchange's options type wins per type. Since each exchange has a distinct options type (`BinanceOptions`, `KucoinOptions`, etc.), this is NOT ambiguous — each type is unique and resolves correctly. The test is valid as written. The CONCERN is stylistic: using `GetRequiredKeyedService<KucoinOptions>(ExchangeId.Kucoin)` would be more explicit about the intent if keyed lookup were supported for options types, but the current unkeyed pattern is the established practice for the codebase (`DiRegistrationTests.cs` does not test options resolution at all). Non-blocking.
- **Fix**: No fix required. Minor note: consider adding a comment to the test explaining that unkeyed options resolution works because each exchange has a distinct options type.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:80`

---

## Summary

- PASS: `AddKucoinExchange` signature — exact parity with `AddOkxExchange`/`AddBitgetExchange` pattern.
- PASS: `CryptoExchangesOptions` additions — four nullable `string?` fields with correct naming convention; non-breaking.
- PASS: `AddCryptoExchanges` return type and null guard — `IServiceCollection` returned, double null guard via aggregator + `ExchangeServiceRegistration`.
- PASS: `KucoinOptions` public shape — `BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds` present.
- PASS: MCP tool-schema unchanged — KuCoin routed via `ExchangeId.Kucoin` with no schema modification.
- PASS: NuGet conventions — `PackageId`, `Description`, `<IsPackable>false</IsPackable>` on test project, `PackageLicenseExpression` inherited.
- PASS: `InternalsVisibleTo` — test and mock assemblies only; no consumer app granted visibility.
- PASS: Missing `using CryptoExchanges.Net.Core;` is intentional — KuCoin uses its own `KucoinSymbolMapper`, not the shared format-based `SymbolMapper`.
- CONCERN: Acceptance criteria says "fail-fast on missing api-key" but no `OptionsValidationException` test for empty `ApiKey` exists; behavior is consistent with all other exchanges (PassThrough gate, not validator) (confidence: 60/100, non-blocking).
- CONCERN: `AddCryptoExchanges_KucoinOptions_AppliesViaAggregator` uses unkeyed `GetRequiredService<KucoinOptions>()` — valid because each exchange has a distinct options type, but a comment would aid clarity (confidence: 55/100, non-blocking).

## Final Verdict

`APPROVED` — All breaking-change checks pass. The implementation is a faithful clone of the OKX/Bitget DI pattern with correct KuCoin-specific variation points. The two concerns are non-blocking, low-confidence style observations consistent with pre-existing patterns across all exchange integrations.
