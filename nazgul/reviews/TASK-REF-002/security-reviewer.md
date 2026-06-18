# Security Review — TASK-REF-002
**Branch**: refactor/interface-seams
**Reviewer**: Security Reviewer (automated)
**Date**: 2026-06-18
**Verdict**: APPROVED

---

## Scope

TASK-REF-002 is a behavior-preserving DIP refactor. Two interface seams are introduced:
- `ISignatureService` (Core.Auth): `string Sign(string payload)` — implemented by all 3 concrete signature services.
- `IExchangeTimeSync` (Core.Resilience): `ComputeOffset` + `ApplyOffset` — `ExchangeTimeSync` converted from `static class` to `public sealed class`.

The review focused on: signing path integrity, secret key handling, credential exposure vectors, and the handler pipeline position (inside/outside Polly retry).

---

## Findings

### Finding: ISignatureService exposes no secret material
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Auth/ISignatureService.cs:1-8`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The interface exposes only `string Sign(string payload)`. No secret key, no HMAC key bytes, no credential data is part of the interface contract. The secret stays inside the concrete implementation's `_secretKeyBytes` (Binance/Bybit) or `_secretKey string` (OKX — pre-existing, unchanged by this diff).

---

### Finding: Binance handler's BuildSignedQuery inlining is byte-identical
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:84-89`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The handler previously called `signatureService.BuildSignedQuery(withTs)` (a method on the concrete `BinanceSignatureService`). Since `BuildSignedQuery` is not part of `ISignatureService`, it was inlined as a private handler method. The inlined logic is:

  ```
  var signature = signatureService.Sign(queryString);
  var separator = string.IsNullOrEmpty(queryString) ? string.Empty : "&";
  return $"{queryString}{separator}signature={signature}";
  ```

  This is character-for-character identical to the original `BinanceSignatureService.BuildSignedQuery` (confirmed against base SHA 3eeb698). The HMAC computation path (`Sign` → `HMACSHA256.HashData(_secretKeyBytes, queryBytes)` → `Convert.ToHexStringLower`) is unchanged. Wire bytes are identical.

---

### Finding: Signing pipeline position unchanged — mark-and-strip pattern preserved
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs`, `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs`, `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. All three handlers still sit inside the Polly retry boundary (signing finalizer position in the pipeline is unchanged — only the ctor param type changed from concrete to interface). The strip-then-sign pattern (StripSigning for Binance query params; Remove/Add header pairs for Bybit/OKX) is completely unchanged. Retry safety is preserved.

---

### Finding: Secret keys not stored in signing handlers
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:13`, `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:18-19`, `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:18-19`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. No handler stores or captures the secret key. Each handler holds only: `apiKey` (string), `signatureService` (ISignatureService — opaque), and `timeOffset` (Func<long>). The secret material lives exclusively inside the concrete signature service instances, which are composer-constructed and never DI-registered.

---

### Finding: No secret in IExchangeTimeSync / ExchangeTimeSync
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Resilience/IExchangeTimeSync.cs`, `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `IExchangeTimeSync` and `ExchangeTimeSync` are purely time-arithmetic: they compute and store a server−local offset in milliseconds. No credential, API key, or HMAC key is present. The `Interlocked.Exchange` atomic write in `ApplyOffset` is preserved exactly from the static implementation.

---

### Finding: DI registration of IExchangeTimeSync uses TryAdd (override-safe)
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:70`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `services.TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()` uses `TryAdd` semantics, meaning a consumer's earlier registration wins. This is the correct pattern — it allows test overrides without being a security concern. No credentials flow through `IExchangeTimeSync`.

---

### Finding: No credential exposure via logging, exceptions, or ToString
- **Severity**: N/A
- **Confidence**: 100
- **File**: All modified files
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. Full grep across all modified signature service files for `ToString`, `log`, `Log`, `JsonSerializer`, `JsonInclude` shows no secret exposure. The only `ToString` call in the diff region is `DateTimeOffset.ToString(format, culture)` for the OKX timestamp — not credential-related. The `ISignatureService` interface has no `ToString()` override risk. `ExchangeCredentials.ToString()` (pre-existing, not modified) already redacts `SecretKey`.

---

### Finding: ISignatureService not registered in DI — correct by design
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs`, all 3 `XxxClientComposer.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `ISignatureService` is intentionally NOT registered in the DI container. Concrete signature service instances are constructed directly by each exchange's composer (e.g., `BinanceClientComposer.BuildResilientHttpClient` at line 73: `BinanceSignatureService? sig = ... new(options.SecretKey)`). This is correct — registering the signature service in DI would risk making the secret key reachable via `IServiceProvider`. The concrete-instance-through-interface pattern preserves the existing encapsulation.

---

### Finding: BinanceSignatureService.BuildSignedQuery remains public on internal class
- **Severity**: LOW
- **Confidence**: 40
- **File**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:27`
- **Category**: Security
- **Verdict**: PASS (pre-existing, out of scope)
- **Issue**: `BuildSignedQuery` is declared `public` on an `internal sealed` class. This is pre-existing (present on base SHA 3eeb698) and has no external surface exposure due to the `internal` class modifier. It is now unreachable from `BinanceSigningHandler` (which inlined the equivalent logic), but it remains on the concrete class. This is a minor cleanliness note, not introduced by this diff.
- **Fix**: Not required for this PR. Could be removed in a follow-up cleanup pass (tracked in issue #14).

---

## Checklist Summary

- **PASS**: Credential safety — `ISignatureService` exposes only `Sign(string)`. No secret bytes in the interface or in any handler. `_secretKeyBytes` stays in the concrete classes.
- **PASS**: Signing integrity — All 3 signing handlers type their ctor param as `ISignatureService` but receive the same concrete instance as before. Pipeline position (inside Polly retry boundary) is unchanged. Mark-and-strip behavior is unchanged.
- **PASS**: Wire bytes — Binance `BuildSignedQuery` inlining is byte-identical to the original method (confirmed line-by-line against base SHA 3eeb698). HMAC-SHA256 computation path is unchanged for all 3 exchanges.
- **PASS**: No logging / exception / ToString exposure of secrets in any new or modified file.
- **PASS**: No new DI registration of signature services (correct — kept composer-constructed).
- **PASS**: `IExchangeTimeSync` is non-security (time offset only). Conversion from static to instance is behavior-identical. `Interlocked.Exchange` atomic write preserved.
- **PASS**: DI override pattern (`TryAddSingleton`) is correct and does not create a credential exposure vector.
- **CONCERN**: `BinanceSignatureService.BuildSignedQuery` is now a dead public method on the internal class (confidence: 40/100, non-blocking, pre-existing, out of scope for this PR).

---

## Final Verdict

**APPROVED**

All security-relevant checks pass with high confidence. The refactor is behavior-preserving: same HMAC-SHA256 computation, same secret-key holding pattern, same per-attempt re-signing, same wire bytes. No new credential exposure vectors were introduced. The two new interfaces (`ISignatureService`, `IExchangeTimeSync`) are correctly scoped — the signing interface carries no secret material; the time-sync interface is non-security. The single low-confidence concern is pre-existing and non-blocking.
