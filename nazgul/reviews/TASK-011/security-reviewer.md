# Security Review — TASK-011: OkxSignatureService + OkxSigningRequest

**Reviewer**: Security Agent
**Date**: 2026-06-18
**Files reviewed**:
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`

---

## Checklist Findings

### Finding 1: No hand-rolled crypto — Core primitive used correctly
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:25-26`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `Sign(string prehash)` delegates entirely to `HmacSignature.Compute(_secretKey, prehash, SignatureEncoding.Base64)`. There is no `HMACSHA256`, no `Encoding.UTF8.GetBytes`, and no `Convert.ToBase64String` in this file. The Bybit reference service (`BybitSignatureService.cs:23-26`) hand-rolls hex; OKX correctly uses the Core primitive from TASK-009 instead.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:38-53`

### Finding 2: Secret-key guard present and robust
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:62-65`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `InitializeSecretKey` uses `ArgumentException.ThrowIfNullOrWhiteSpace(secretKey)` before returning the value. A null, empty, or whitespace secret throws at construction time; a blank secret cannot silently produce a weak signature. The guard mirrors the Binance reference pattern exactly (`BinanceSignatureService.cs:37-41`).
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:37-41`

### Finding 3: Secret not logged, serialized, or embedded in exceptions
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs` (entire file)
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The file contains no `ToString()` override, no `JsonSerializer`/`[JsonInclude]`, no logger usage, and no exception message that interpolates `_secretKey`. The secret is stored as a `private readonly string _secretKey` field and flows only into `HmacSignature.Compute` as the first argument.

### Finding 4: Secret not transmitted — signature returned to caller
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:25-26` and XML doc (lines 11-14)
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `Sign()` returns the base64 signature string. The `<remarks>` block explicitly documents that the signature goes into the `OK-ACCESS-SIGN` header (added by the later signing handler). The secret is never placed in a query parameter or any returned value.

### Finding 5: Prehash binds method, path+query, and body — replay resistance confirmed
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:50-57`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `BuildPrehash` assembles `timestamp + METHOD.ToUpperInvariant() + requestPath + body`. The timestamp is ISO-8601 UTC with milliseconds (anti-replay window). The method is upper-cased, binding the verb so a signed GET cannot be replayed as POST. The `requestPath` is documented to include the query string for GET requests, binding query parameters into the signature. The body is required non-null (empty string valid), binding POST bodies. This matches OKX's specified prehash format.

### Finding 6: ISO-8601 UTC timestamp — not epoch
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:59-60`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `FormatTimestamp` uses `"yyyy-MM-ddTHH:mm:ss.fffZ"` with `CultureInfo.InvariantCulture`, producing exactly the form `2026-06-17T12:00:00.000Z` that OKX requires. `.ToUniversalTime()` is called before formatting, preventing local-timezone offset leakage. This is not epoch-ms (which would be wrong for OKX).

### Finding 7: No duplicate-timestamp / stale-signature risk from OkxSigningRequest
- **Severity**: N/A (PASS)
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: The marker pattern correctly mirrors the Binance/Bybit approach. `MarkSigned` calls `request.Options.Set(SignedKey, true)` (idempotent), and `IsSigned` reads via `TryGetValue`. Unlike Binance (which appends query params and must strip them on retry), OKX places credentials in headers; the signing handler (TASK-012) is responsible for overwriting the header on each attempt. The marker itself introduces no stale-signature risk — it only signals intent. Importantly, the key string `"okx.signed"` is distinct from `"binance.signed"` and `"bybit.signed"`, preventing cross-exchange marker collisions if multiple handlers are ever composed in the same pipeline.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs`

### Finding 8: Body validation — empty string allowed for GET/DELETE
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:55`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `body` is guarded with `ArgumentNullException.ThrowIfNull` only (not `ThrowIfNullOrWhiteSpace`), which is correct — an empty body is the valid value for GET and DELETE requests. Treating an empty body as an error would break all read-only requests.

### Finding 9: OkxSigningRequest is internal — no public surface exposure
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs:5`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. Both new types are `internal`. `OkxSignatureService` is `internal sealed`; `OkxSigningRequest` is `internal static`. Callers outside the assembly cannot directly access signing primitives. This matches the `BinanceSigningRequest` access modifier (`internal static`). Note: `BybitSigningRequest` was observed to be `public static` — OKX correctly reverts to `internal`, tightening the surface.

### Finding 10: No `[JsonInclude]` / serialization path on secret storage
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs` (entire file)
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `OkxSignatureService` is not a config/options class; it is a service. There are no attributes, no properties, and no serialization entry points. The `_secretKey` field is private.

---

## Concern (Non-blocking)

### Finding C1: `BuildPrehash` accepts a blank `requestPath` — leading slash not enforced
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:48, 56`
- **Category**: Security (signature integrity)
- **Verdict**: CONCERN (non-blocking — confidence 55)
- **Issue**: `BuildPrehash` validates that `requestPath` is non-null/non-empty/non-whitespace, but does not assert a leading `/`. OKX's prehash specification requires a leading slash (e.g. `/api/v5/market/tickers?instType=SPOT`). A caller supplying `api/v5/…` (without the slash) would produce a silently invalid prehash that OKX would reject with a signature error. This is a caller-contract concern rather than an internal security defect; the XML doc does specify "including the leading `/`".
- **Fix**: Optionally add a `Debug.Assert(requestPath.StartsWith('/'))` or throw `ArgumentException` if `!requestPath.StartsWith('/')`. The latter is the more defensive choice for a public-facing (even if internal) API boundary. The signing handler (TASK-012) should also ensure it builds the path correctly.
- **Pattern reference**: OKX API documentation (prehash format); no existing codebase pattern covers this specific validation.

---

## Summary

| # | Verdict | Item | Reason |
|---|---------|------|--------|
| 1 | PASS | No hand-rolled crypto | Delegates entirely to `HmacSignature.Compute(..., SignatureEncoding.Base64)` |
| 2 | PASS | Secret-key guard | `ArgumentException.ThrowIfNullOrWhiteSpace` in `InitializeSecretKey` at construction |
| 3 | PASS | Secret not logged/serialized | No `ToString`, no `JsonSerializer`, no exception embedding |
| 4 | PASS | Secret not transmitted | Signature returned to caller; goes to header in later handler |
| 5 | PASS | Prehash integrity | Binds timestamp+METHOD+requestPath+body; ISO-8601 anti-replay |
| 6 | PASS | ISO-8601 UTC timestamp | Correct format string; `ToUniversalTime()` guards TZ offset |
| 7 | PASS | Retry safety | Marker is idempotent; unique key prevents cross-exchange collision |
| 8 | PASS | Empty body allowed | `ThrowIfNull` only (not `ThrowIfNullOrWhiteSpace`) — correct for GET |
| 9 | PASS | Internal access level | Both types `internal`; narrows exposure vs Bybit `public` |
| 10 | PASS | No serialization exposure | No options class, no attributes, `_secretKey` private |
| C1 | CONCERN | Leading slash on requestPath | Not enforced; silently invalid prehash if caller omits it (confidence: 55/100, non-blocking) |

---

## Final Verdict

**APPROVED**

No blocking findings. All ten security checks pass at confidence 97-100. The single non-blocking concern (C1) is a defensive-programming suggestion at confidence 55 — the XML documentation already specifies the correct calling convention and the actual rejection would come from OKX's server rather than silently corrupting security properties. The OKX signature service is a clean, correct implementation that delegates crypto to the Core primitive, guards the secret at construction, keeps the secret out of all observable surfaces, and returns the signature to the caller rather than appending it to the request.
