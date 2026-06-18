# Security Review — TASK-015 (OKX signing/DI)

**Reviewer**: Security Reviewer
**Date**: 2026-06-18
**Verdict**: APPROVED

---

## Five-Question Answers

### Q1. Finalizer gate — BOTH SecretKey AND Passphrase?
YES. The gate checks both in two places, using identical logic:

- **Factory-free path** (`OkxClientComposer.BuildResilientHttpClient`, line 221):
  `(string.IsNullOrEmpty(options.SecretKey) || string.IsNullOrEmpty(options.Passphrase))`
  → `PassThroughHandler` if either is missing; `OkxSigningHandler` only when both are present.

- **DI path** (`ServiceCollectionExtensions.cs`, `requestFinalizerFactory`, line 811):
  `if (string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase))`
  → same guard, same outcome.

Both paths return `OkxSigningHandler` only when neither is null/empty. PASS.

### Q2. Secret/passphrase leakage?
None found.

- `OkxOptions` has no `ToString()` override — the compiler-synthesized one for plain `class` does NOT enumerate properties, so no leakage.
- `OkxOptions` has no `[JsonInclude]`, `[JsonProperty]`, or `[DataMember]` attributes; serialization path is clean.
- `OkxErrorTranslator` error text is `"OKX error {code}: {msg}"` or `"OKX HTTP {status}"` — no credential strings.
- `OkxSigningHandler` error messages on guard failures name the header names (`OK-ACCESS-KEY`, `OK-ACCESS-PASSPHRASE`), not the actual values.
- `CryptoExchangesOptions` in DI assembly exposes `OkxSecretKey` and `OkxPassphrase` as plain properties with no `[JsonInclude]`; however this class is the SAME posture as `BinanceSecretKey`/`BybitSecretKey` already in the codebase — accepted pattern.

PASS.

### Q3. Signed-vs-unsigned classification correct?
Confirmed via `OkxHttpClient` and the service methods:

- **Unsigned (public)**: `/api/v5/market/ticker`, `/api/v5/market/tickers`, `/api/v5/market/books`, `/api/v5/market/candles`, `/api/v5/market/trades`, `/api/v5/public/time`, `/api/v5/public/instruments` — all call `GetAsync` with `signed: false`.
- **Signed (private)**: `/api/v5/trade/order` (place/cancel/get), `/api/v5/trade/orders-pending`, `/api/v5/trade/orders-history`, `/api/v5/trade/cancel-order`, `/api/v5/trade/cancel-batch-orders`, `/api/v5/trade/fills`, `/api/v5/account/balance` — all call with `signed: true` (PostAsync defaults to `true`; GetAsync explicit `true`).

Matches OKX V5 auth requirements. PASS.

### Q4. Verbatim wire body — sign and send the same bytes?
YES, for both overloads.

- **Dict overload** (`PostAsync<T>(string, Dictionary<string,string>?, ...)`): serializes to `json` string via `JsonSerializer.Serialize`, passes to `PostJsonAsync`. The `StringContent` is built from the same `json` string. The signing handler reads back `Content.ReadAsStringAsync()` from the same `StringContent` — verbatim.
- **Object-body overload** (`PostAsync<T>(string, object, ...)`): identical path: `JsonSerializer.Serialize(body, JsonOptions)` → `PostJsonAsync` → `StringContent` → signing handler reads it back. No re-serialization between sign and send.

The `PostJsonAsync` helper (lines 74–83) is the single shared codepath; both overloads converge there. PASS.

### Q5. Re-sign on retry — fresh signature+timestamp per attempt?
YES. `OkxSigningHandler` sits BELOW the Polly retry boundary (it is the `requestFinalizer`, which is inner to Polly). On every attempt it:
1. Calls `DateTimeOffset.UtcNow.AddMilliseconds(timeOffset())` — fresh instant.
2. `ReadAsStringAsync` — reads the current content.
3. Computes a new `prehash` and calls `signatureService.Sign(prehash)`.
4. Strips all four `OK-ACCESS-*` headers (`Remove`) before adding the new ones — exactly the mark-and-strip pattern.

No stale reuse possible. PASS.

---

## Findings

### Finding: OkxOptions has no ToString() redaction
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `OkxOptions` is a plain `class` (not a `record`), so the default `ToString()` does not enumerate properties. There is no active leakage today. However, unlike Binance's `BinanceOptions`, there is no explicit `ToString()` override that redacts `SecretKey` and `Passphrase`. If a developer ever logs `options.ToString()` it is safe today, but a future refactor to `record` would silently expose secrets.
- **Fix**: Add a `ToString()` override that returns a redacted representation, e.g.: `public override string ToString() => $"OkxOptions {{ ApiKey={ApiKey[..Math.Min(4, ApiKey.Length)]}***, SecretKey=[redacted], Passphrase=[redacted] }}"`. Reference: `BinanceOptions` pattern (if it has one) or add consistently with the codebase's existing redaction approach.
- **Pattern reference**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:69-92` (existing credential properties follow same no-override pattern — concern applies equally to Binance/Bybit options, but is non-blocking).

### Finding: ReadFromJsonAsync called without content-type check or JsonException guard
- **Severity**: LOW
- **Confidence**: 50
- **File**: `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs:47,82,94`
- **Category**: Security (error info leakage path)
- **Verdict**: CONCERN (non-blocking — confidence < 50 blocking threshold; already handled upstream)
- **Issue**: `ReadFromJsonAsync<T>` is called on every successful response without checking `Content-Type` or catching `JsonException`. If OKX returns a plain-text error page (e.g. a Cloudflare HTML block on HTTP 200), this throws an untyped `JsonException` rather than a typed SDK exception. The raw HTML response could contain IP/routing info. However, the resilience pipeline translates non-2xx to typed exceptions before reaching this code, so the risk surface is narrow (only unexpected 200 HTML from a proxy/CDN).
- **Fix**: Wrap `ReadFromJsonAsync` in a try/catch for `JsonException` and rethrow as a typed `ExchangeApiException` with the raw body. Reference: `BinanceErrorTranslator.cs:36-50`.

---

## Summary

- PASS: Finalizer gate — both `SecretKey` AND `Passphrase` checked via `||` in both the container-free and DI paths; `PassThroughHandler` on either missing.
- PASS: No leakage — no credentials in error text, exception messages, or `ToString`; no `[JsonInclude]` on secret fields.
- PASS: Signed-vs-unsigned correct — all public market-data endpoints unsigned; all account/trade endpoints signed.
- PASS: Verbatim body — single `PostJsonAsync` codepath; no re-serialization; signing handler reads back the same `StringContent`.
- PASS: Re-sign on retry — fresh `UtcNow` + offset per attempt; mark-and-strip (`Remove` before `Add`) for all four `OK-ACCESS-*` headers.
- CONCERN: `OkxOptions` missing `ToString()` redaction (confidence: 55/100, non-blocking).
- CONCERN: `ReadFromJsonAsync` without `JsonException` guard (confidence: 50/100, non-blocking).

## Final Verdict

**APPROVED** — All five security checks pass. Two non-blocking concerns noted; neither is a blocking security defect.
