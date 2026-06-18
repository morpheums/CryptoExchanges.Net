# Security Review: TASK-014 — OkxHttpClient + IOkxHttpClient

**Reviewer**: Security Reviewer (automated)
**Date**: 2026-06-18
**Branch**: feat/m3-okx
**Files under review**:
- `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs`
- `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs`

**Supporting reads** (for verification, not under review):
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Okx/OkxOptions.cs`
- `src/CryptoExchanges.Net.Http/ResilientHttpClientServiceCollectionExtensions.cs`

---

## 1. SIGN-CONSISTENCY: Signed path equals wire path (PathAndQuery)

**GET and DELETE** construct the URL via `BuildUrl(endpoint, parameters)` which returns `endpoint?{escaped-qs}` or just `endpoint`. When combined with the host-only `BaseAddress`, the resulting `HttpRequestMessage.RequestUri.PathAndQuery` is exactly the path+query the client assembled — no transformation by the runtime, because relative-URI resolution against a path-less base (`https://www.okx.com`) preserves the path as-is. `OkxSigningHandler` then reads `request.RequestUri!.PathAndQuery` verbatim for the prehash. The signed string and the wire path are byte-for-byte identical. PASS.

**POST** sets `new HttpRequestMessage(HttpMethod.Post, endpoint)` with no query string (parameters become the JSON body). `RequestUri.PathAndQuery` is just the endpoint path — e.g., `/api/v5/trade/order` — which is what the handler uses as `requestPath` in the prehash. The body is set as `StringContent` with UTF-8 encoding; the handler reads it back with `ReadAsStringAsync` which also returns the UTF-8 string. The serializer that writes and the reader that reads are using the same in-memory `StringContent` buffer — there is no second serialization step — so the signed body equals the wire body. PASS.

---

## 2. Credential Safety — No secrets in client

`OkxHttpClient` has a single constructor parameter: `HttpClient httpClient`. No `ApiKey`, `SecretKey`, or `Passphrase` is stored in a field, passed to a constructor, or used anywhere in the client. Signing credentials are handled exclusively by `OkxSigningHandler` (a `DelegatingHandler` on the injected `HttpClient`). The client calls only `OkxSigningRequest.MarkSigned(request)` to set an options flag — no credential is touched. PASS.

---

## 3. Input Validation on Endpoint

All three public methods (`GetAsync`, `PostAsync`, `DeleteAsync`) begin with `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)`. This matches the Binance pattern reference at `BinanceHttpClient.cs:30/55/65`. PASS.

---

## 4. Query String Escaping — Keys AND Values

`BuildQueryString` at lines 110–120 applies `Uri.EscapeDataString(kvp.Key)` AND `Uri.EscapeDataString(kvp.Value)`. Both key and value are escaped before concatenation. This matches the established pattern at `BinanceHttpClient.cs:96`. No string-concatenation injection surface exists. PASS.

---

## 5. No Inline Signing / No Bypass of MarkSigned

Signed requests are flagged via `OkxSigningRequest.MarkSigned(request)` (line 71 for GET, line 87 for POST, line 99 for DELETE when `signed == true`). When `signed == false` (default on GET), `MarkSigned` is not called and the handler's `IsSigned` check returns false, causing the handler to pass the request through untouched. No signing logic exists inline in the client. PASS.

---

## 6. Mark-and-Strip on Retry

The OKX signing model uses headers rather than query parameters (unlike Binance which appends `signature=` to the query). `OkxSigningHandler.ResignAsync` (lines 67–74 of OkxSigningHandler.cs) calls `request.Headers.Remove(...)` for all four `OK-ACCESS-*` headers before adding fresh ones. This correctly prevents duplicate header values on retry. The OKX design does not need a `MarkSigned`/strip-query dance because headers are always replaced, not accumulated. The pattern is sound for headers. PASS.

---

## 7. POST Body: No Double-Serialization / Mutation Risk

`PostAsync` at line 58: `var json = JsonSerializer.Serialize(parameters ?? [], JsonOptions)`. This produces a single serialized string, which is wrapped in `StringContent`. The `OkxSigningHandler` reads the body back via `request.Content.ReadAsStringAsync(ct)` (OkxSigningHandler.cs:59) — this reads the `StringContent`'s buffer directly, yielding the identical string. There is no second serialization, no compression, no buffering transformation between the write and the read. Signature covers exactly the wire bytes. PASS.

---

## 8. No Logging of Sensitive Data

No `ILogger`, no `Console.Write`, no `Debug.Write`, no `ToString()` override touching ApiKey/SecretKey/Passphrase anywhere in `OkxHttpClient.cs` or `IOkxHttpClient.cs`. PASS.

---

## 9. JSON Deserialization Safety (ReadFromJsonAsync without try/catch)

### Finding: ReadFromJsonAsync without JsonException guard

- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs:47,63,75`
- **Category**: Security (JSON deserialization safety)
- **Verdict**: CONCERN (non-blocking — confidence 60 < 80)
- **Issue**: All three public methods call `response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)` with no `try/catch` for `JsonException`. If the pipeline's `ErrorTranslationHandler` does not intercept all non-2xx responses before reaching this deserialization step, a plain-text error page (e.g., a Cloudflare WAF 403, or OKX returning a non-JSON body on overload) would throw an untyped `JsonException` rather than a typed `ExchangeApiException`. The comment in the class XML doc says "any response that reaches this type is already a success" — meaning the `ErrorTranslationHandler` sitting below Polly should have already thrown a typed exception for non-2xx responses. This is architecturally correct per the pipeline ordering confirmed in `ResilientHttpClientServiceCollectionExtensions.cs:67`. However, there is no explicit content-type check, and if a 2xx response arrives with a non-JSON body (edge case on some exchanges), deserialization would throw an unguarded `JsonException`. The Binance reference (`BinanceHttpClient.cs:34`) has the same pattern with no guard, so this is a known accepted trade-off in this codebase.
- **Fix**: If content-type safety is desired, wrap the `ReadFromJsonAsync` call in a `try/catch (JsonException)` and rethrow as `ExchangeApiException`. Alternatively, add an `EnsureSuccessStatusCode()` call or verify the `Content-Type` header before deserializing. Non-blocking given existing architecture.
- **Pattern reference**: `BinanceHttpClient.cs:34` (same pattern, no guard — consistent with codebase convention)

---

## 10. OkxOptions: Secrets Not Serializable / No ToString Leak

`OkxOptions` (`OkxOptions.cs`) does not carry `[JsonInclude]`, has no `ToString()` override, and has no serialization path. The `SecretKey` field is `string SecretKey { get; set; }` with no attributes — it will not be serialized unless the caller explicitly serializes the options object. No auto-serialization risk within this client. (Noted as out of scope for TASK-014, verified for completeness.) PASS.

---

## Summary

- PASS: SIGN-CONSISTENCY — Handler reads `RequestUri.PathAndQuery`; client builds path identically for GET/DELETE (via `BuildUrl`) and POST (endpoint directly). Signed path == wire path byte-for-byte.
- PASS: Credential safety — No ApiKey/SecretKey/Passphrase stored or referenced in `OkxHttpClient`. Signing delegated entirely to `OkxSigningHandler`.
- PASS: Input validation — `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` at every public method boundary.
- PASS: Query string escaping — `Uri.EscapeDataString` applied to both key and value in `BuildQueryString`.
- PASS: No inline signing bypass — `MarkSigned` called correctly; unsigned requests skip the handler cleanly.
- PASS: Retry header strip — `OkxSigningHandler` removes all four `OK-ACCESS-*` headers before re-adding on each attempt.
- PASS: POST body integrity — Single serialization to `StringContent`; handler reads the same buffer back; no mutation risk.
- PASS: No logging/credential exposure — No logger, no `ToString`, no exception message carrying secrets.
- CONCERN: `ReadFromJsonAsync` without `JsonException` guard — confidence 60/100, non-blocking. Architecturally safe given the pipeline ordering but no explicit content-type validation. Consistent with Binance pattern in this codebase.

---

VERDICT: APPROVED, overall confidence 95
