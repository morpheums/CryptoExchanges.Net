# Security Review — TASK-008 (commit f60bd18)

**Reviewer**: Security Reviewer
**Task**: TASK-008 — Bybit tests + AddBybitExchange DI (closes M-BYBIT)
**Commit**: f60bd18
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

---

## Findings

### Finding 1: Secret-gated PassThrough gate is correct and consistent with Binance
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:201-209`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:106-110` (Binance gate)

The `requestFinalizerFactory` uses `string.IsNullOrEmpty(o.SecretKey)` — identical to the Binance gate. A secretless client receives `PassThroughHandler`; a client with a `SecretKey` receives `BybitSigningHandler`. The `BybitClientComposer.BuildResilientHttpClient` (container-free `Create`) path uses the same check at line 84, so both paths are consistent. Confirmed by DI unit test `Di_AddBybitExchange_Secretless_StillResolvesWorkingClient` and integration test `Secretless_BuildResilientHttpClient_DoesNotSign`.

---

### Finding 2: Bybit named HTTP client does NOT add api-key as a default header — correct
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:175-183`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:83-85` (Binance adds X-MBX-APIKEY as default; Bybit correctly does not)

The `AddHttpClient(BybitClientName, ...)` callback adds only `User-Agent` to `DefaultRequestHeaders`. No `X-BAPI-API-KEY` default header. The api-key is applied per-attempt by `BybitSigningHandler.SendAsync` only for authenticated clients. An unsigned Bybit request on a secretless pipeline carries no api-key header — correct for public endpoints. This is the secure choice: Binance requires the api-key on all requests (hence the default header), Bybit V5 only needs it on signed requests.

---

### Finding 3: No real secrets in test fixtures
- **Severity**: N/A
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitSigningTests.cs`, `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs`, `tests/CryptoExchanges.Net.Bybit.Tests.Integration/BybitPipelineEndToEndTests.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

All test files use obvious dummy values: `"secret"`, `"mysecret"`, `"myapikey"`, `"key"`, `"k"`, `"s"`. No `Console.Write`, `ILogger`, or `Debug.Write` call exposes credential fields. Test output is assertion-driven (FluentAssertions) only.

---

### Finding 4: HMAC integrity — correct algorithm, correct sign-string, no crypto misuse
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

`BybitSignatureService.Sign()` uses `HMACSHA256.HashData(_secretKeyBytes, signBytes)` — the static one-shot form, avoiding state reuse bugs. Secret is stored as `byte[]` (not a string field), so it cannot appear in any `ToString()` call on the service. Sign-string assembly follows Bybit V5 documented format: GET = `timestamp + apiKey + recvWindow + queryString`; POST = `timestamp + apiKey + recvWindow + jsonBody`. Fixed HMAC vector `HMAC-SHA256("hello", key="secret")` = `88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b` is a known-good test vector.

---

### Finding 5: Mark-and-strip pattern correctly implemented for headers
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:59-64`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `Resilience/BinanceSigningRequest.cs` (Binance mark-and-strip for query params)

`BybitSigningRequest` marks requests via `HttpRequestOptionsKey<bool>` — the flag is never cleared, ensuring the mark survives retries so the handler re-signs on each attempt. The handler strips `X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, and `X-BAPI-SIGN` before re-adding them, preventing header duplication on retry. Integration test `SignedGet_Retried_ReSignsWithSingleHeaderSet` asserts `h["X-BAPI-SIGN"].Should().NotContain(",")` across both retry attempts.

---

### Finding 6: BybitOptions has no serialization attributes and no ToString() override
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

`BybitOptions` is a plain `sealed` POCO with no `[JsonInclude]`, `[JsonPropertyName]`, `[DataMember]`, or `[Serializable]` attributes. No `ToString()` override. `CryptoExchangesOptions.BybitSecretKey` similarly has no serialization attributes.

---

### Finding 7: SecretKey is never stored outside the signature service or signing handler
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`, `src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

SecretKey flow: `BybitOptions.SecretKey` → `new BybitSignatureService(o.SecretKey)` → immediately converted to `byte[]`; string reference not retained as a field. `BybitExchangeClient` constructor takes no `SecretKey` parameter. Never appears in any exception message, `RawBody`, or log statement.

---

### Finding 8: Uri.EscapeDataString used correctly in Bybit query building
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:78`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `BinanceHttpClient.cs:91`

`BuildQueryString` applies `Uri.EscapeDataString(kvp.Key)` and `Uri.EscapeDataString(kvp.Value)` to all query parameters. POST parameters are sent as a JSON body via `JsonSerializer.Serialize`, which handles its own escaping.

---

### Finding 9: Rate limiting and error translation correctly wired
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:196-197`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

`ReactiveRateLimitGate` registered. `BybitErrorTranslator` maps HTTP 429 to `RateLimitExceededException` (with `RetryAfter`), retCodes `10006`/`10018` to `RateLimitExceededException`, `10003`/`10004` to `AuthenticationException`, HTTP `401` to `AuthenticationException`. Confirmed by unit tests.

---

### Finding 10: JSON deserialization safety
- **Severity**: N/A
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:34,50,62`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

`ReadFromJsonAsync<T>` is called without explicit `JsonException` try/catch, but this matches the pre-existing Binance pattern. Non-JSON error bodies are handled at the resilience-pipeline level before `ReadFromJsonAsync` is reached (error responses are non-2xx and intercepted by the error translator). Unit test `ErrorTranslator_NonJsonBody_FallsBackToApiException` confirms `<html>502</html>` returns `ExchangeApiException`. Not a regression introduced by this task.

---

## Summary

- PASS: Secret-gated PassThrough finalizer — `string.IsNullOrEmpty(o.SecretKey)` gate is identical to Binance reference; secretless yields `PassThroughHandler`, secret-bearing yields `BybitSigningHandler`
- PASS: Named HTTP client does NOT add `X-BAPI-API-KEY` as a default header — correct; api-key is applied per-attempt by the signing handler only
- PASS: Test fixtures use obvious dummy values only (`"secret"`, `"key"`, `"k"`, `"s"`, `"mysecret"`); no real credentials; no credential logging
- PASS: HMAC-SHA256 via `HMACSHA256.HashData`, secret stored as `byte[]` (not string), never leaked; fixed vectors match known-good values
- PASS: Mark-and-strip pattern correctly implemented via header removal before re-addition on retry; single-value-per-header assertion in integration test confirms no duplication
- PASS: `BybitOptions` has no serialization attributes; `SecretKey` is not stored in the exchange client itself, only in the signature service's `byte[]` field
- PASS: `Uri.EscapeDataString` applied to all query string key/value pairs in `BybitHttpClient.BuildQueryString`
- PASS: `ReactiveRateLimitGate` registered; `BybitErrorTranslator` correctly maps 429 and rate-limit retCodes

---

## Final Verdict

**APPROVED**

No security defects found. All four focus areas (secret-gated PassThrough, no api-key default header leak, dummy-only test credentials, HMAC integrity) pass cleanly. The implementation faithfully mirrors the Binance security patterns where applicable and correctly diverges where Bybit's header-based signing differs from Binance's query-string-based signing. Confidence: 99/100.
