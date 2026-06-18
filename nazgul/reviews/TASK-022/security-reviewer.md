# Security Review: TASK-022 — Bitget FINAL milestone closer

**Verdict**: APPROVED
**Confidence**: 94
**Reviewer model**: claude-sonnet-4-6
**Date**: 2026-06-18

---

## Executive Summary

All blocking security criteria pass. The HMAC signing pipeline is correct, secret and passphrase are never stored outside the signing handler/service, the mark-and-strip retry pattern is properly implemented, `JsonElement.ValueKind` guards are in place throughout the error translator, and the `"00000"` success-code path is correctly handled. Two non-blocking concerns are noted (ToSting redaction gap on mutable options classes, and a minor information surface in error text from the `msg` field), neither of which reaches the 80-confidence blocking threshold.

---

## Blocking Findings (REJECT)

None.

---

## Full Findings

### Finding: Secret confined to signing service — no leakage path
- **Severity**: N/A
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:11-17`, `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs:88-93`, `src/CryptoExchanges.Net.Bitget/ServiceCollectionExtensions.cs:62-68`
- **Verdict**: PASS
- **Issue**: The `SecretKey` field is passed only to `new BitgetSignatureService(secretKey)` which stores it in `private readonly string _secretKey`. No field outside the signing service class holds it. The `BitgetSigningHandler` receives an `ISignatureService` abstraction — it never sees the raw secret.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs`

### Finding: Partial-credential gate correctly routes to PassThroughHandler
- **Severity**: N/A
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs:88-93`, `src/CryptoExchanges.Net.Bitget/ServiceCollectionExtensions.cs:62-68`
- **Verdict**: PASS
- **Issue**: Both code paths (container-free `BuildResilientHttpClient` and DI `requestFinalizerFactory`) apply the identical gate: `string.IsNullOrEmpty(options.SecretKey) || string.IsNullOrEmpty(options.Passphrase)` → `PassThroughHandler`. A missing secret OR missing passphrase produces a no-op finalizer. The `BitgetSigningHandler` constructor also has a secondary guard in `ResignAsync` that throws `InvalidOperationException` if either credential is empty at sign-time, providing defense-in-depth if a handler is ever constructed directly without full credentials.
- **Pattern reference**: Integration test `PassphraseMissing_SignedRequest_FastFails` confirms the secondary guard; `Secretless_OrPassphraseless_BuildResilientHttpClient_DoesNotSign` confirms the primary gate.

### Finding: No accidental signing of public endpoints
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:46`, `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:28`
- **Verdict**: PASS
- **Issue**: `GetAsync` with `signed: false` does not call `BitgetSigningRequest.MarkSigned(request)`. The `BitgetSigningHandler` only calls `ResignAsync` when `BitgetSigningRequest.IsSigned(request)` returns true. All market-data endpoints (`/api/v2/spot/market/...`, `/api/v2/spot/public/...`) are called with `signed: false` and all account/trading endpoints with `signed: true`. The `IBitgetHttpClient` default for `GetAsync` is `signed = false`, while `PostAsync` defaults to `signed = true`.

### Finding: Mark-and-strip pattern correctly prevents duplicate headers on retry
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:68-77`
- **Verdict**: PASS
- **Issue**: Before adding each set of four ACCESS-* headers, `ResignAsync` calls `request.Headers.Remove(...)` for all four (`ACCESS-KEY`, `ACCESS-SIGN`, `ACCESS-TIMESTAMP`, `ACCESS-PASSPHRASE`). This is the exact strip-before-add pattern. The pipeline position is correct: the signing handler sits between the Polly retry layer and the `ErrorTranslationHandler` (innermost), which means on each retry the request passes through the signing handler again, strips the prior headers, and computes a fresh timestamp and signature.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs` (same Remove/Add pattern for query params)

### Finding: HMAC prehash format — timestamp + UPPER(method) + path + ?query + body
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:25-35`, `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:52-65`
- **Verdict**: PASS
- **Issue**: `BuildPrehash` produces `timestamp + method.ToUpperInvariant() + requestPath + ('?'+queryString if non-empty) + body`. The signing handler correctly uses `request.RequestUri!.AbsolutePath` for `requestPath` and `request.RequestUri.Query.TrimStart('?')` for `queryString`, keeping signed and transmitted query strings byte-identical. POST body is read from `request.Content` before sending. Base64 output is confirmed via the test vector `Sign_ProducesExpectedBase64ForFixedVector` and the `^[A-Za-z0-9+/]+={0,2}$` regex assertion in `SignedGet_SetsFourAccessHeaders`.

### Finding: "00000" success code is not treated as an error
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetErrorTranslator.cs:31-33`
- **Verdict**: PASS
- **Issue**: The translator has an explicit early-return for `code == SuccessCode` (where `SuccessCode = "00000"`) that returns a plain `ExchangeApiException` rather than any typed sub-exception. The test `ErrorTranslator_SuccessCode_IsNotAnError` confirms this path. The `Translate` method is only invoked by `ErrorTranslationHandler` on non-success HTTP responses, so the success-code path is an edge case (HTTP-level error with success-shaped body) — but it is handled correctly and does not throw.

### Finding: JsonElement.ValueKind guarded before GetString() in error translator (ADR-001 conv 3)
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetErrorTranslator.cs:84-87`
- **Verdict**: PASS
- **Issue**: The `ReadString` helper checks `v.ValueKind == JsonValueKind.String` before calling `v.GetString()`. A `code` or `msg` field carrying a number (`{"code":40037,...}`) or null (`{"msg":null}`) returns `null` rather than throwing `InvalidOperationException`. The tests `ErrorTranslator_MalformedFields_DoNotThrow` exercise all three malformed-field cases (numeric code, numeric msg, null msg). `JsonException` from `JsonDocument.Parse` is caught and returns `(null, null)`.

### Finding: URI query parameters escape via Uri.EscapeDataString
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:109`
- **Verdict**: PASS
- **Issue**: `BuildQueryString` applies `Uri.EscapeDataString` to both key and value for every parameter. No new endpoint path or query parameter value bypasses this.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:91`

### Finding: Rate limit gate registered; 429 and exchange-specific codes map correctly
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs:81`, `src/CryptoExchanges.Net.Bitget/Resilience/BitgetErrorTranslator.cs:42-43`
- **Verdict**: PASS
- **Issue**: `new CryptoExchanges.Net.Http.ReactiveRateLimitGate()` is instantiated and passed to `HttpClientPipelineBuilder.Build`. The error translator maps HTTP 429 and Bitget-specific codes `"429"`, `"30007"`, `"40404"` to `RateLimitExceededException` with `RetryAfter` from response headers.

### Finding: ReadFromJsonAsync only reached on 2xx responses
- **Severity**: N/A
- **Confidence**: 96
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:48,81,93`
- **Verdict**: PASS
- **Issue**: The `ErrorTranslationHandler` is the innermost handler (closest to the transport). It reads the body as a plain string, calls `translator.Translate(response, body)`, and throws a typed exception for any 4xx business error before the response returns to `BitgetHttpClient`. Success responses (2xx) pass through to `ReadFromJsonAsync`. 5xx/408/429/418 transients are passed through and become `ExchangeConnectivityException` or `RateLimitExceededException` at the `TransientExhaustionHandler` layer, again before `ReadFromJsonAsync`. Therefore `ReadFromJsonAsync` is only called on genuine 2xx success responses, which are well-formed JSON by the exchange's contract.

### Finding: No secret, passphrase, or computed signature in exception messages or logs
- **Severity**: N/A
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:41-44`, `src/CryptoExchanges.Net.Bitget/Resilience/BitgetErrorTranslator.cs:27`
- **Verdict**: PASS
- **Issue**: Exception messages in `ResignAsync` contain only string literals (`"Bitget signed request requires an API key..."` and `"...requires a passphrase..."`), no credential values. The error translator produces `text = $"Bitget error {code}: {msg}"` where `code` and `msg` come from the exchange's own error envelope (public exchange content), not from any SDK-internal credential or signature value. No logging framework is used anywhere in the Bitget assembly.

### Finding: No [JsonInclude] or serialization attribute on BitgetOptions/CryptoExchangesOptions secrets
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs`, `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:71-113`
- **Verdict**: PASS
- **Issue**: `BitgetOptions` and `CryptoExchangesOptions` are plain `sealed class` types (not `record`), no `[JsonSerializable]`, `[JsonInclude]`, or `[JsonPropertyName]` attributes on any credential field. The default `object.ToString()` for a class does not emit property values, so no accidental serialization path exists.

---

## Non-Blocking Concerns

### CONCERN: BitgetOptions and CryptoExchangesOptions lack a redacting ToString() override

- **Severity**: LOW
- **Confidence**: 60 (non-blocking — below 80 threshold)
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs`, `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:71-113`
- **Issue**: `BitgetOptions` (which carries `SecretKey` and `Passphrase`) and `CryptoExchangesOptions` (which carries `BitgetSecretKey` and `BitgetPassphrase`) are plain `sealed class` types without a `ToString()` override. The default `Object.ToString()` returns the type name only (not property values), so accidental string interpolation of the options object does not leak secrets. However, the codebase's own `ExchangeCredentials` demonstrates the established pattern: override `ToString()` to explicitly redact secrets. The peer exchange options (`OkxOptions`, `BybitOptions`) also lack this override, so this is not a regression introduced by TASK-022 — it is a pre-existing gap across the codebase.
- **Fix suggestion**: Add a `ToString()` override to `BitgetOptions` returning `$"BitgetOptions {{ ApiKey = {Mask(ApiKey)}, SecretKey = [REDACTED], Passphrase = [REDACTED] }}"`. Track as a separate hardening task. Not blocking because the default `ToString()` does not emit property values.

### CONCERN: BitgetOptions.ToCredentials() throws on empty passphrase — unused but present

- **Severity**: LOW
- **Confidence**: 45 (non-blocking — well below 80 threshold)
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:24-25`
- **Issue**: `ToCredentials()` passes `Passphrase` (which could be `string.Empty`) to `new ExchangeCredentials(ApiKey, SecretKey, Passphrase)`. `ExchangeCredentials` calls `ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)` when passphrase is non-null, but `string.Empty` is treated as non-null. A caller invoking `options.ToCredentials()` when the passphrase is empty would get an `ArgumentException`. The signing path explicitly avoids `ToCredentials()` (the gate checks `string.IsNullOrEmpty(options.Passphrase)` before constructing a handler) and no test calls `ToCredentials()` with an empty passphrase. Risk: a future caller unfamiliar with the constraint accidentally calls this method and encounters a confusing exception.
- **Fix suggestion**: The `ToCredentials()` method is a pre-existing API, but adding an XML doc warning or making it `internal` (it is currently public via the public `BitgetOptions` class) would reduce this hazard. Not blocking because no current code path exercises the broken case.

---

## Summary Table

| Category | Item | Verdict |
|---|---|---|
| Secret confinement | SecretKey stored only in BitgetSignatureService | PASS |
| Partial-credential gate | PassThrough when SecretKey OR Passphrase missing | PASS |
| No accidental public-endpoint signing | signed=false bypasses MarkSigned | PASS |
| Mark-and-strip retry pattern | All 4 headers removed before each re-add | PASS |
| HMAC prehash format | timestamp+UPPER(method)+path+?query+body, base64 | PASS |
| Success code "00000" | Not treated as error | PASS |
| JsonElement.ValueKind guards | ReadString checks ValueKind before GetString | PASS |
| Query string escaping | Uri.EscapeDataString on all params | PASS |
| Rate limit gate | ReactiveRateLimitGate registered | PASS |
| 429/rate-limit codes | Correctly map to RateLimitExceededException | PASS |
| ReadFromJsonAsync JSON safety | Only called on 2xx (error path pre-screened) | PASS |
| Secret/sig leakage in exceptions | No credential or signature value in messages | PASS |
| Options serialization safety | No [JsonInclude]; class not record | PASS |
| ToString redaction on options | No override (default hides props) | CONCERN (confidence: 60/100, non-blocking) |
| ToCredentials() empty passphrase | Unused but throws ArgumentException | CONCERN (confidence: 45/100, non-blocking) |

---

## Final Verdict

**APPROVED** — Confidence: 94

All security-critical checks pass with high confidence. No blocking findings (confidence >= 80 AND severity HIGH/MEDIUM). Both non-blocking concerns are below the 80-confidence threshold and represent cosmetic hardening opportunities, not live vulnerabilities. The HMAC signing pipeline, partial-credential gating, mark-and-strip retry pattern, ValueKind-guarded JSON reads, and "00000" success-code handling all meet the security requirements for this milestone.
