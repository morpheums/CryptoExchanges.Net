# Code Review: TASK-008 — Bybit Tests + AddBybitExchange DI

**Reviewer**: Code Reviewer (test quality focus)
**Commit**: f60bd18
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

---

## Final Verdict

**APPROVED**

---

## Findings

### Finding: HMAC-SHA256 fixed vectors are cryptographically pinned
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitSigningTests.cs:24-61`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: All three vectors independently verified with HMAC-SHA256 (Python hmac module). `Sign_ProducesExpectedHexForFixedVector` pins `"88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b"`. GET and POST sign-string vectors each pin a distinct concrete hex string. These are not "does not throw" checks — they are cryptographically grounded and would catch any change to the signing algorithm, key/message encoding, or sign-string construction order.

---

### Finding: GET/POST sign-string assembly asserts concatenation order
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitSigningTests.cs:33-61`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: Each test asserts the intermediate sign-string as a concrete literal (`"1700000000000myapikey5000category=spot&symbol=BTCUSDT"`) before hashing. The production `BuildGetSignString` at `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:43` concatenates in `timestamp+apiKey+recvWindow+queryString` order. A reordering would cause both the intermediate string assertion and the hex assertion to fail.

---

### Finding: Regression (a) — limit-clamp to 50 genuinely covers production behavior
- **Severity**: N/A (PASS)
- **Confidence**: 98
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs:215-251`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: Both `Trading_GetOrderHistory_DefaultLimit_ClampsToFifty` and `Account_GetTradeHistory_DefaultLimit_ClampsToFifty` use `Arg.Do<Dictionary<string,string>>` to capture the outgoing HTTP params dict and assert `captured!["limit"].Should().Be("50")`. The production code at `BybitTradingService.cs:207` and `BybitAccountService.cs:116` both execute `Math.Min(limit, BybitRequestValidation.MaxHistoryLimit)` where `MaxHistoryLimit = 50`. With the IExchangeClient default `limit = 500`, `Math.Min(500, 50) = 50`. If the clamp were removed, `ValidateHistoryWindow(500, ...)` at `BybitRequestValidation.cs:25` would throw `ArgumentOutOfRangeException` (limit > 50), failing the `NotThrowAsync()` assertion. The regression is genuinely covered and cannot pass against broken production code.

---

### Finding: Regression (b) — cancel-by-clientId re-fetch path genuinely covered
- **Severity**: N/A (PASS)
- **Confidence**: 97
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs:256-287`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: Mock returns `BybitOrderAck { OrderId = string.Empty, OrderLinkId = "cli-77" }`. Production `CancelOrderByClientIdAsync` at `BybitTradingService.cs:156-157` sets `canceledId = response.Result?.OrderId ?? string.Empty = ""` then calls `FetchOrderAsync(wireSymbol, canceledId: "", ct, orderLinkId: "cli-77")`. `FetchOrderAsync` at lines 244-247 only adds `parameters["orderId"]` when `!string.IsNullOrEmpty(orderId)` — skipped for `""` — then falls through to `else if (!string.IsNullOrEmpty(orderLinkId)) parameters["orderLinkId"] = "cli-77"`. The test captures `refetchParams` from the `/v5/order/realtime` call and asserts `ContainKey("orderLinkId").WhoseValue.Should().Be("cli-77")` and `NotContainKey("orderId")`. `order.OrderId.Should().Be("real-99")` is a concrete non-empty id, not a null or "any non-null" check. Removing the empty-orderId branch from `FetchOrderAsync` would send `orderId=""` to Bybit and return a different result, breaking the assertion.

---

### Finding: Re-sign-on-retry test correctly asserts single header set per attempt
- **Severity**: N/A (PASS)
- **Confidence**: 96
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Integration/BybitPipelineEndToEndTests.cs:112-140`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `SeqStub` returns HTTP 500 on first call, 200 on second, forcing exactly one retry. `HttpClientPipelineBuilder.Build` positions the signing handler below Polly's `ResilienceHandler` (confirmed at `HttpClientPipelineBuilder.cs:38-44`); on retry Polly re-invokes the full inner chain, re-executing `BybitSigningHandler.ResignAsync`. `ResignAsync` at `BybitSigningHandler.cs:59-64` calls `request.Headers.Remove(...)` before `Add(...)` for all three BAPI headers. `SeqStub` snapshots headers into a list before returning each response. The `foreach (var h in stub.Headers)` loop asserts `h["X-BAPI-SIGN"].Should().NotContain(",")` on both entries — a comma would appear if the previous attempt's header were not stripped before re-adding. `ErrorTranslationHandler` passes 5xx through unchanged (confirmed at `ErrorTranslationHandler.cs:27`), and Polly's `ShouldHandle` retries transient 5xx on GET requests (confirmed at `ExchangeResiliencePipeline.cs:42-43`). Mechanism is structurally sound.

---

### Finding: Re-sign test does not assert timestamps differ between attempts
- **Severity**: LOW
- **Confidence**: 60
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Integration/BybitPipelineEndToEndTests.cs:136-137`
- **Category**: Testing
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The test verifies each attempt carries a single, non-doubled `X-BAPI-TIMESTAMP` value but does not assert `stub.Headers[0]["X-BAPI-TIMESTAMP"] != stub.Headers[1]["X-BAPI-TIMESTAMP"]`. A "fresh timestamp on each retry" guarantee would require comparing the two captured values.
- **Fix** (suggested, non-blocking): Add `stub.Headers[0]["X-BAPI-TIMESTAMP"].Should().NotBe(stub.Headers[1]["X-BAPI-TIMESTAMP"])` after the loop. In practice the two attempts always land on different wall-clock milliseconds, so the test would pass; the assertion is simply absent.
- **Pattern reference**: The primary invariant (single header set, no doubling) is correct and enforced; this is an additive enhancement.

---

### Finding: Error translator covers all mapped retCodes with concrete type assertions
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitSigningTests.cs:277-323`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: Theory covers `AuthenticationException`, `InsufficientBalanceException`, `InvalidOrderException`, `RateLimitExceededException`, and unknown-code `ExchangeApiException` with concrete retCode/status pairs. `RetryAfter` asserted as `TimeSpan.FromSeconds(7)` (concrete). `ErrorTranslator_SuccessEnvelope_IsNotARateLimitOrAuthError` is valid: FluentAssertions `BeOfType<T>` checks exact runtime type (not `IsAssignableFrom`), so it passes only when the result is exactly `ExchangeApiException`; `RateLimitExceededException` and `AuthenticationException` are `sealed` subclasses, so the positive `BeOfType<ExchangeApiException>` implicitly excludes them.

---

### Finding: DeltaMapper profile tests assert concrete field values, not just "maps without error"
- **Severity**: N/A (PASS)
- **Confidence**: 98
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs:39-128`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `OrderProfile_MapsAllScalarsAndResolvesSymbol` asserts 12 distinct fields including timestamps as `DateTimeOffset.FromUnixTimeMilliseconds(1700000000000)` and `StopPrice.Should().BeNull()` for a zero trigger price. `BalanceProfile_FreeIsWalletMinusLocked` asserts `Free=1.5m`, `Locked=0.25m`, `Total=1.75m` (Total is computed property `Free + Locked` per `Models.cs:110`; arithmetic is consistent). `TickerProfile_ScalesPercentAndComputesChange` asserts `PriceChangePercent=2.5m` (0.025 × 100) and `PriceChange=1000m`. No vacuous tests.

---

### Finding: DI resolution tests cover keyed singleton, scope safety, invalid-options fail-fast, and cross-exchange resolution
- **Severity**: N/A (PASS)
- **Confidence**: 97
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs:292-356`
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `Di_AddBybitExchange_MapperIsKeyedSingleton` uses `BeSameAs` for reference equality — meaningful singleton check. `Di_AddBybitExchange_InvalidOptions_FailFast` asserts `OptionsValidationException` on `TimeoutSeconds=0` — concrete typed exception. `Di_AddBybitExchange_IsScopeClean` uses `ValidateScopes=true, ValidateOnBuild=true` — genuine scope-safety guard. `Di_AddCryptoExchanges_ResolvesBybitAndBinance` verifies both keyed registrations coexist. `await using` used correctly for `IAsyncDisposable` service providers.

---

### Finding: All tests execute cleanly, no regressions
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: All test projects
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `dotnet test --filter 'Category!=Integration'` → 212 passes (77 Bybit.Unit + 68 Core.Unit + 12 Http.Unit + 10 DI.Unit + 45 Binance.Integration), 0 failures. `dotnet test --filter 'Category=Integration'` → 5/5 Bybit.Integration passes. No Binance regression.

---

## Summary

- PASS: HMAC-SHA256 fixed vectors — cryptographically pinned, independently verified
- PASS: Sign-string assembly — concatenation order asserted as literal strings
- PASS: Regression (a) limit-clamp — `Arg.Do` capture + `"50"` assertion; would fail if clamp removed
- PASS: Regression (b) cancel-by-clientId re-fetch — `orderLinkId` param presence asserted; `orderId` absence asserted; `"real-99"` concrete value asserted
- PASS: Re-sign-on-retry — single header set per attempt (NotContain ","); StripSigning mechanism verified in production source
- PASS: Error translator — all retCodes mapped to exact typed exceptions with concrete inputs
- PASS: DeltaMapper profiles — 12+ concrete field assertions per profile, no vacuous tests
- PASS: DI resolution — singleton, scope-safe, fail-fast, cross-exchange
- PASS: All 217 tests pass, no regressions
- CONCERN: Re-sign test (`BybitPipelineEndToEndTests.cs:136-137`) missing cross-attempt timestamp inequality assertion (confidence: 60/100, non-blocking)
