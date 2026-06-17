# Code Review: TASK-002 — BybitSignatureService + BybitSigningRequest

**Reviewer**: Code Reviewer (C# 13 / .NET 10)
**Date**: 2026-06-17
**Commit**: 5654d93
**Branch**: feat/m2-exchange-expansion
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`

**Pattern references consulted**:
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs`

---

## Build & Test Verification

- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s).**
- `dotnet test` (Category!=Integration) → **135 passed, 0 failed, 0 skipped.**

---

## Findings

### Finding 1: `BuildGetSignString` and `BuildPostSignString` — missing guards on all four string parameters

- **Severity**: MEDIUM
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37-53`
- **Category**: Correctness
- **Verdict**: REJECT (blocking — severity MEDIUM, confidence 85)
- **Issue**: Both `public static` sign-string builder methods accept four string parameters (`timestamp`, `apiKey`, `recvWindow`, `queryString`/`jsonBody`) with no input guards. Passing null or whitespace silently produces a malformed sign-string (e.g., `"null<apiKey><recvWindow><qs>"`), which generates a bad signature sent to the Bybit API with no exception raised at the call site. The project rule is unambiguous: "ArgumentException.ThrowIfNullOrWhiteSpace(param) for strings — SymbolMapper.cs:76" and "These are NOT optional. Missing guards on public API are HIGH severity."
- **Fix**: Add the following at the top of `BuildGetSignString` before the return statement:
  ```csharp
  ArgumentException.ThrowIfNullOrWhiteSpace(timestamp);
  ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
  ArgumentException.ThrowIfNullOrWhiteSpace(recvWindow);
  ArgumentException.ThrowIfNullOrWhiteSpace(queryString);
  ```
  Apply the same (substituting `jsonBody`) to `BuildPostSignString`.
- **Pattern reference**: `SymbolMapper.cs:76`, `BinanceSignatureService.cs:38-39`.

---

### Finding 2: `Sign(string signString)` — missing `ArgumentException.ThrowIfNullOrWhiteSpace` guard

- **Severity**: MEDIUM
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:22-27`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence 70)
- **Issue**: `Sign(string signString)` has no guard on `signString`. Passing null causes a `NullReferenceException` inside `Encoding.UTF8.GetBytes` rather than an `ArgumentException` at the public boundary. However, `BinanceSignatureService.Sign(string queryString)` (the direct pattern reference, `BinanceSignatureService.cs:18`) also omits this guard. Because the Binance pattern is treated as normative in this review, confidence is reduced to 70 — but the omission is still a gap versus the stated project convention.
- **Fix**: Add `ArgumentException.ThrowIfNullOrWhiteSpace(signString);` as the first line of `Sign`. If the Binance pattern is intentionally laissez-faire about `Sign`'s input, update `BinanceSignatureService` in a follow-up and note the deviation; otherwise add the guard to both services now.
- **Pattern reference**: `SymbolMapper.cs:76` (guard on string params), `BinanceSignatureService.cs:18-22` (acknowledged pattern debt).

---

### Finding 3: `BybitSigningRequest` — missing `<param>` tags on `MarkSigned` and `IsSigned`

- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:9-21`
- **Category**: XML documentation
- **Verdict**: CONCERN (non-blocking — confidence 60)
- **Issue**: `MarkSigned(HttpRequestMessage request)` and `IsSigned(HttpRequestMessage request)` each take a `request` parameter with no `/// <param name="request">` tag. With `GenerateDocumentationFile=true` the generated XML will have no parameter description. However, `BinanceSigningRequest.cs` (the pattern reference) also omits `<param>` tags on these methods — this is a consistent omission rather than a new regression.
- **Fix**: Add `/// <param name="request">The outgoing HTTP request message to inspect or modify.</param>` to both methods. Optionally add `/// <returns>` to `IsSigned` and `/// <exception cref="ArgumentNullException">` to both. Update `BinanceSigningRequest` in parity.
- **Pattern reference**: `BinanceSigningRequest.cs:9-22` (consistent missing param docs).

---

### Finding 4: HMAC-SHA256 primitive correctness

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:24-26`
- **Verdict**: PASS
- **Notes**: `HMACSHA256.HashData(_secretKeyBytes, signBytes)` + `Convert.ToHexStringLower(hash)` is the correct static method form introduced in .NET 6+, avoids the `IDisposable` HMAC instance entirely, and produces lowercase hex. UTF-8 encoding of the sign-string is correct per Bybit's API spec. Matches `BinanceSignatureService.cs:20-22` exactly.

---

### Finding 5: Sign-string concatenation order

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:39, 52`
- **Verdict**: PASS
- **Notes**: `$"{timestamp}{apiKey}{recvWindow}{queryString}"` (GET) and `$"{timestamp}{apiKey}{recvWindow}{jsonBody}"` (POST) match the acceptance criteria exactly: `timestamp + apiKey + recvWindow + queryString` (GET) and `timestamp + apiKey + recvWindow + jsonBody` (POST). No signature is appended to the sign-string or to any payload.

---

### Finding 6: `BybitSigningRequest.IsSigned` idempotency and round-trip

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:17-21`
- **Verdict**: PASS
- **Notes**: `TryGetValue(SignedKey, out var v) && v` returns false for an unsigned request (key absent) and true after `MarkSigned` sets it to `true`. Calling `MarkSigned` twice is idempotent (second `Options.Set` overwrites with the same value). Mirrors `BinanceSigningRequest.cs:19-20` exactly.

---

### Finding 7: CS1574 avoidance in `BybitSigningRequest` summary

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:3-4`
- **Verdict**: PASS
- **Notes**: Using plain text "the Bybit signing handler" instead of `<see cref="BybitSigningHandler"/>` is the correct approach when the referenced type does not yet exist. An unresolvable cref is a build warning promoted to error under `TreatWarningsAsErrors`. The manifest documents this deviation explicitly.

---

### Finding 8: Sealed class, primary constructor, `readonly` field, `_camelCase` naming

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13-15`
- **Verdict**: PASS
- **Notes**: `sealed class` with primary constructor `(string secretKey)` and `private readonly byte[] _secretKeyBytes = InitializeSecretKey(secretKey)` matches the Binance pattern at `BinanceSignatureService.cs:9-11` exactly. `_camelCase` field naming is correct per codebase convention (`SymbolMapper.cs:16-22`).

---

### Finding 9: `InitializeSecretKey` guard

- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:55-59`
- **Verdict**: PASS
- **Notes**: `ArgumentException.ThrowIfNullOrWhiteSpace(secretKey)` is present and correct. Matches `BinanceSignatureService.cs:38-39`.

---

### Finding 10: Build and test verification

- **Severity**: N/A
- **Confidence**: 100
- **Verdict**: PASS
- **Notes**: `dotnet build CryptoExchanges.Net.sln` produces 0 Warning(s), 0 Error(s). All 135 unit tests pass. No regressions introduced.

---

## Summary

| # | Verdict | Severity | Confidence | Item |
|---|---------|----------|------------|------|
| 1 | REJECT | MEDIUM | 85 | `BuildGetSignString`/`BuildPostSignString` missing guards on all 4 string params each |
| 2 | CONCERN | MEDIUM | 70 | `Sign(string signString)` missing `ThrowIfNullOrWhiteSpace` guard (matches Binance debt) |
| 3 | CONCERN | LOW | 60 | Missing `<param>` tags on `MarkSigned`/`IsSigned` (consistent with Binance pattern) |
| 4 | PASS | — | 100 | HMAC-SHA256 primitive correct (`HashData` + `ToHexStringLower`) |
| 5 | PASS | — | 100 | Sign-string concatenation order matches acceptance criteria exactly |
| 6 | PASS | — | 100 | `IsSigned`/`MarkSigned` idempotency and round-trip correct |
| 7 | PASS | — | 100 | CS1574 avoidance documented and correct |
| 8 | PASS | — | 100 | `sealed`, primary constructor, `_camelCase` field — all correct |
| 9 | PASS | — | 100 | `InitializeSecretKey` guard correct |
| 10 | PASS | — | 100 | Build clean (0 warnings, 0 errors); all unit tests pass |

---

## Final Verdict

**FINAL VERDICT: CHANGES_REQUESTED**

One blocking finding (confidence 85, severity MEDIUM): `BuildGetSignString` and `BuildPostSignString` expose four public string parameters each with no input guards. The project rule — "ArgumentException.ThrowIfNullOrWhiteSpace — NOT optional — HIGH severity" — is unambiguous. Silently accepting null or whitespace inputs produces malformed sign-strings without raising an exception at the public boundary.

The fix is mechanical: add four `ArgumentException.ThrowIfNullOrWhiteSpace` calls at the top of each static builder method. Everything else — the HMAC primitive, concatenation order, BybitSigningRequest structure, build cleanliness, and structural conformance with the Binance pattern — is correct and ready to ship.
