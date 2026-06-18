# Security Review — TASK-021: BitgetHttpClient + IBitgetHttpClient

Reviewer: security-reviewer
Date: 2026-06-18
Files reviewed:
- src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs
- src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs

Reference (read-only):
- src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs
- src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs
- src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs

---

## Findings

### Finding 1: Sign-consistency — GET/DELETE query string re-encoding by HttpClient URI parser
- **Severity**: LOW
- **Confidence**: 40
- **File**: `BitgetHttpClient.cs:96-106` (BuildUrl / BuildQueryString)
- **Category**: Security / Signing Integrity
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `BuildUrl` returns a plain string like `/api/v2/spot/market/tickers?symbol=BTCUSDT_SPBL`. When `HttpRequestMessage` is constructed with this relative URI string and combined with `HttpClient.BaseAddress` (`https://api.bitget.com`), the .NET `Uri` class parses the result. For well-formed percent-encoded values produced by `Uri.EscapeDataString`, the .NET Uri parser preserves the encoding in `RequestUri.Query` unchanged. However, if a future caller passes a parameter value containing characters that `Uri.EscapeDataString` does NOT encode (e.g. `~`, `-`, `_`, `.`, digits, and unreserved ASCII letters) the round-trip is still safe. The risk would arise only if the Uri class re-encoded a `%XX` sequence — which it does NOT do for correctly-escaped input. At confidence 40 this is an observation rather than a defect: the contract works as described in the XML doc provided `BaseAddress` is host-root-only. Because the BaseAddress constraint is documented but not enforced in code yet (DI registration is a later task), a misconfigured BaseAddress with a trailing path component could alter `AbsolutePath`, breaking prehash byte-consistency. This is not a security vulnerability in the submitted files; it is a configuration-time hazard.
- **Fix**: When the DI registration task arrives, enforce that `BaseAddress` ends with no path component (i.e. `new Uri(options.BaseUrl)` has an empty or `/`-only path) or add an assertion in `BitgetHttpClient`'s constructor. Example guard: `if (httpClient.BaseAddress?.AbsolutePath is not ("/" or "")) throw new InvalidOperationException(...)`. This keeps the invariant load-bearing rather than documentation-only.
- **Pattern reference**: `BitgetHttpClient.cs:7-35` (XML doc contract described but not enforced in constructor)

---

### Finding 2: POST `ReadFromJsonAsync` — no content-type or JsonException guard
- **Severity**: LOW
- **Confidence**: 55
- **File**: `BitgetHttpClient.cs:54, 81, 87, 93` (all `ReadFromJsonAsync` call sites)
- **Category**: JSON Deserialization Safety
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `ReadFromJsonAsync<T>` throws `JsonException` if the exchange returns a plain-text body (e.g. an HTTP 502 error page, or a gateway timeout with `text/html`). The resilience pipeline is expected to catch Bitget-specific error envelopes and translate them before the response reaches `BitgetHttpClient`, but if an intermediate proxy or CDN returns a non-JSON error that the pipeline passes through (e.g. because it is a 2xx envelope wrapping an HTML body), `JsonException` propagates to the caller as an untyped exception rather than a typed SDK exception. Confidence is 55 — whether this can actually occur depends on the pipeline ordering defined in a later DI task; if the error translator gate is comprehensive it may not matter in practice.
- **Fix**: Wrap `ReadFromJsonAsync` in a try/catch for `JsonException` (and optionally check `Content-Type` header) to surface a typed `ExchangeApiException` with the raw body instead of leaking `JsonException`. Reference pattern: `BinanceErrorTranslator.cs:36-50`.
- **Pattern reference**: Binance pattern in `src/CryptoExchanges.Net.Binance/` error translator

---

## Checklist Pass/Fail Summary

### PASS items

- **Credential safety — no secrets in client**: `BitgetHttpClient` accepts only `HttpClient`; no `apiKey`, `secretKey`, or `passphrase` field is present. All credential handling is in `BitgetSigningHandler`. PASS.
- **No logging of ApiKey / SecretKey**: No `ILogger`, no `ToString()` override, no `JsonSerializer` on credentials, no exception message construction involving credentials. PASS.
- **No secrets in IBitgetHttpClient interface**: Interface is `internal`, no credential parameters on any method. PASS.
- **No inline signing**: `BitgetHttpClient` calls only `BitgetSigningRequest.MarkSigned(request)` — a flag-setter with no HMAC logic. All HMAC/header work is in `BitgetSigningHandler`. PASS.
- **Signing integrity — MarkSigned called on all signed paths**: `GetAsync` (line 46), `PostJsonAsync` (line 79, shared by both PostAsync overloads), and `DeleteAsync` (line 91) all call `MarkSigned` when `signed == true`. No path bypasses the marker. PASS.
- **No duplicate timestamp/signature**: Bitget uses headers (not query params) for signing credentials; `BitgetSigningHandler` strips and re-adds headers on each attempt (lines 70-77 of handler). `BitgetHttpClient` does not add any signing headers itself. PASS.
- **Query string escaping**: `BuildQueryString` applies `Uri.EscapeDataString` to both key and value for every parameter (line 109). PASS.
- **Endpoint input validation**: `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` present at the top of all four public methods (lines 44, 56, 67, 89). PASS.
- **No string concatenation of unescaped user input into URL**: Parameters go through `BuildQueryString → Uri.EscapeDataString`; the endpoint itself is validated (not user-supplied at the transport layer). PASS.
- **IBitgetHttpClient is internal**: Both interface and implementation are `internal`. PASS.
- **No serialization of secrets**: `JsonSerializerOptions` are for HTTP response deserialization only; no credentials are in any serialization path. PASS.
- **POST body is verbatim wire body**: `StringContent` is constructed once from the `json` string; the handler reads it back via `ReadAsStringAsync` before any mutation. `MarkSigned` is called after `Content` is set, so the body is stable when signed. PASS.
- **ConfigureAwait(false)**: Every `await` uses `.ConfigureAwait(false)`. PASS.

### CONCERN items

- **CONCERN**: BaseAddress host-root constraint documented but not enforced in constructor — confidence 40, non-blocking. See Finding 1.
- **CONCERN**: `ReadFromJsonAsync` has no `JsonException` guard — if a non-JSON 2xx-wrapped body reaches the client it surfaces as an untyped exception. Confidence 55, non-blocking. See Finding 2.

### REJECT items

None.

---

## Summary

- PASS: Credential isolation — client holds no secrets, all signing in handler
- PASS: MarkSigned on all signed code paths, no bypass
- PASS: No duplicate header injection (headers, not query params; handler strips before re-adding)
- PASS: Uri.EscapeDataString on both key and value for all query parameters
- PASS: All public method boundaries validated with ThrowIfNullOrWhiteSpace/ThrowIfNull
- PASS: POST body constructed once, stable between MarkSigned and SendAsync, verbatim on wire
- CONCERN: BaseAddress host-root invariant — documented, not code-enforced (confidence: 40/100, non-blocking)
- CONCERN: ReadFromJsonAsync — no JsonException guard for non-JSON responses (confidence: 55/100, non-blocking)

---

## Final Verdict

**APPROVED**

No blocking security issues. The implementation faithfully mirrors the OKX pattern, correctly isolates credential handling to the signing handler, applies EscapeDataString to all query parameter values, validates all entry points, and ensures byte-consistency between what the client builds and what the handler signs. Both concerns are configuration-time hazards or defensive-coding improvements appropriate for a future hardening pass, not defects in the submitted files.
