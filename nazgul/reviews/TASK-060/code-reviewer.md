---
verdict: APPROVE
---

# Code Review — TASK-060: `AddKucoinExchange` DI Registration + `AddCryptoExchanges` + MCP Wiring

## Build & Test Verification

- `dotnet build CryptoExchanges.Net.sln` — **0 warnings, 0 errors** with `TreatWarningsAsErrors=true`.
- `dotnet test` (unit filter `Category=Unit`, Kucoin project) — **11 passed, 0 failed**.

---

## Findings

### Finding: `Throw<Exception>()` in `AddKucoinExchange_BaseUrlWithPath_FailFast` is not maximally specific
- **Severity**: LOW
- **Confidence**: 55
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs:81`
- **Category**: Testing
- **Verdict**: PASS (non-blocking — confidence below 80, and established pattern in codebase)
- **Issue**: The test asserts `act.Should().Throw<Exception>()` rather than a more specific exception type (e.g., `ArgumentException` or `OptionsValidationException`). A more specific type would catch a regression where the throw changes to a wrong exception kind.
- **Fix**: If `ExchangeUrl.NormalizeHostRoot` always throws `ArgumentException` or wraps it into `OptionsValidationException` on resolution, narrow to that type. If the throw type is genuinely implementation-specific, `Throw<Exception>()` is acceptable.
- **Pattern reference**: `tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetMappingAndServiceTests.cs:558` — this exact same `Throw<Exception>()` pattern is used for the equivalent Bitget test and was accepted in previous reviews, so this is not a regression.

### Finding: `──` section-separator comments in `KucoinDiTests.cs`
- **Severity**: LOW
- **Confidence**: 35
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs:19,59,84,106,119`
- **Category**: Style
- **Verdict**: PASS
- **Issue**: The `// ── Section Name ──` banner comments are present throughout the test file. The LEAN comment mandate flags banner separators. However, identical separators appear in every peer test file in this codebase (`BitgetMappingAndServiceTests.cs`, `OkxMappingAndServiceTests.cs`), making this an established project convention rather than a violation introduced here.
- **Fix**: No action required — consistent with the established project pattern.

### Finding: Missing `using CryptoExchanges.Net.Core;` compared to Bitget/OKX peer
- **Severity**: LOW
- **Confidence**: 40
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:1-8`
- **Category**: Code Quality
- **Verdict**: PASS
- **Issue**: The Bitget and OKX `ServiceCollectionExtensions.cs` files both include `using CryptoExchanges.Net.Core;` (which covers `SymbolMapper` and format types like `BitgetSymbolFormat`). The Kucoin file omits it because KuCoin uses a bespoke `KucoinSymbolMapper` that internally wraps `SymbolMapper`, so the using is not needed at the registration site. No compiler error or warning results — this is correct.
- **Fix**: None — the omission is intentional and correct.

---

## Code Quality Assessment

### `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs`

- **XML docs**: Present and complete — class `<summary>`, `AddKucoinExchange` with `<param>` and `<returns>`, `KucoinClientName` constant with `<summary>`. All public/private members documented. PASS.
- **Pattern fidelity**: Implementation is a character-for-character port of `BitgetServiceCollectionExtensions` with correct KuCoin substitutions — `ExchangeId.Kucoin`, `KucoinClientName`, `KucoinOptions`, `KucoinSymbolMapper`, `KucoinClientComposer`, `KucoinErrorTranslator`, `KucoinSigningHandler`, `KucoinSignatureService`, `KucoinHttpClient`. PASS.
- **Signing gate**: `string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase)` correctly gates both credentials, consistent with the documented KuCoin three-credential requirement and matching Bitget/OKX. PASS.
- **`PassThroughHandler` fallback**: Correct — avoids calling `ToCredentials()` (which throws on empty passphrase) in the DI path. PASS.
- **One-type-per-file**: `ServiceCollectionExtensions.cs` contains only `ServiceCollectionExtensions`. PASS.
- **No banner/noise comments**: The method-level comment block explains non-obvious design decisions (why `configureHttpClient` is null, why the gate avoids `ToCredentials()`). These are not restatements — they explain exchange-specific constraints. PASS.

### `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`

- **Kucoin block added correctly**: The `AddKucoinExchange` delegate maps all four options fields (`BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`) using the `opt.X = options.Y ?? opt.X` null-coalesce pattern, consistent with the OKX/Bitget blocks above it. PASS.
- **`ArgumentNullException.ThrowIfNull(services)`**: Present (line 29 in the full file). PASS.

### `src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs`

- **Four new properties added**: `KucoinBaseUrl`, `KucoinApiKey`, `KucoinSecretKey`, `KucoinPassphrase` — all `string?`, all with XML `<summary>`. PASS.
- **Parity with OKX/Bitget blocks**: OKX and Bitget both expose BaseUrl + ApiKey + SecretKey + Passphrase; Kucoin matches exactly. PASS.

### `.csproj` changes

- **`CryptoExchanges.Net.DependencyInjection.csproj`**: Added `CryptoExchanges.Net.Kucoin` `ProjectReference` in the correct position (after Bitget, before Http). PASS.
- **`CryptoExchanges.Net.Mcp.csproj`**: Added `CryptoExchanges.Net.Kucoin` `ProjectReference` in the correct position. PASS.

### `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs`

- **XML doc on class**: Present. PASS.
- **`[Trait("Category", "Unit")]`**: Present. PASS.
- **Keyed resolution**: `AddKucoinExchange_ResolvesKeyedClient` — covers basic keyed resolution. PASS.
- **Secretless path**: `AddKucoinExchange_Secretless_StillResolvesWorkingClient` — covers `PassThroughHandler` gate with no credentials. PASS.
- **Partial credentials (key + secret, no passphrase)**: `AddKucoinExchange_PassphraseMissing_StillResolves` — covers the specific KuCoin three-credential gate that differs from two-credential exchanges. This test is unique to KuCoin vs Bitget peer tests and is a genuine addition. PASS.
- **ValidateOnStart fail-fast**: `AddKucoinExchange_InvalidOptions_FailFast_TimeoutZero` — uses `OptionsValidationException` specifically. PASS.
- **BaseUrl path fail-fast**: `AddKucoinExchange_BaseUrlWithPath_FailFast` — exercises `ExchangeUrl.NormalizeHostRoot`. PASS (see note on `Throw<Exception>` above).
- **Singleton mapper**: `AddKucoinExchange_MapperIsKeyedSingleton` — resolves the mapper twice and checks reference equality. PASS.
- **No unkeyed registration**: `AddKucoinExchange_NoUnkeyed_ExchangeClient_Registered` — asserts `GetService<IExchangeClient>()` returns null. PASS.
- **Scope graph validity**: `AddKucoinExchange_IsScopeClean` — uses `ValidateScopes + ValidateOnBuild`. PASS.
- **Aggregator resolution**: `AddCryptoExchanges_ResolvesKucoinClient` — spot-checks KuCoin via aggregator. PASS.
- **All-five-exchanges**: `AddCryptoExchanges_ResolvesAllFiveExchanges` — verifies Binance, Bybit, OKX, Bitget, KuCoin all resolve after the fifth exchange is added. This test has high regression value. PASS.
- **Options delegation path**: `AddCryptoExchanges_KucoinOptions_AppliesViaAggregator` — verifies `CryptoExchangesOptions.KucoinApiKey/SecretKey/Passphrase` flow into `KucoinOptions`. PASS.
- **Test naming convention**: All test names follow `MethodName_StateUnderTest_ExpectedBehavior`. PASS.
- **No `Thread.Sleep` or real network calls**: None present. PASS.
- **LR-005 (unit test coverage)**: `AddKucoinExchange` is covered by 10 dedicated tests. PASS.

---

## Summary

- PASS: `ServiceCollectionExtensions.cs` (Kucoin) — exact structural parity with Bitget/OKX peer, correct XML docs, correct signing gate, correct `PassThroughHandler` fallback.
- PASS: `CryptoExchangesOptions.cs` — four new nullable properties, all documented, consistent with OKX/Bitget parity.
- PASS: `ServiceCollectionExtensions.cs` (DI aggregator) — Kucoin block added with correct four-field null-coalesce delegation.
- PASS: `.csproj` changes — `ProjectReference` additions in both DI and MCP projects are well-formed and correctly ordered.
- PASS: `KucoinDiTests.cs` — 10 tests across keyed resolution, secretless path, passphrase-missing gate, fail-fast (timeout), fail-fast (BaseUrl path), singleton mapper, no-unkeyed, scope clean, aggregator spot-check, all-five-exchanges, and options delegation. Exceeds coverage of peer Bitget DI tests.
- CONCERN: `Throw<Exception>()` in `BaseUrlWithPath_FailFast` — non-maximally specific, but matches the identical Bitget pattern at `BitgetMappingAndServiceTests.cs:558` accepted in a prior review cycle (confidence: 55/100, non-blocking).

## Final Verdict

**APPROVED** — All checks pass. Build is clean, all 11 unit tests pass, implementation is a faithful ADR-001-compliant port of the established Bitget pattern with correct KuCoin-specific substitutions. The single concern about `Throw<Exception>()` is a pre-existing codebase pattern, not a regression.
