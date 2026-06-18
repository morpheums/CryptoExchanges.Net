# Code Review: TASK-018 — BitgetSignatureService + BitgetSigningRequest

**VERDICT: APPROVED**
**Overall confidence: 95/100**
**Blocking items: 0**

---

## Build verification

`dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s). Confirmed.

---

## Findings

### Finding: OKX `BuildPrehash` has verbose `<param>`/`<returns>`/`<exception>` docs; Bitget does not — Bitget is MORE correct post-ADR-001 conv 7
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:19-28`
- **Category**: Style
- **Verdict**: PASS
- **Issue**: None — the lean `<summary>`-only doc on `BuildPrehash` aligns with ADR-001 convention 7 (lean comments; skip `<param>`/`<returns>` when self-explanatory). The OKX version is the over-documented outlier, not the reference.
- **Fix**: N/A

### Finding: `HmacSignature.Compute` guards payload with `ThrowIfNullOrWhiteSpace` — could `BuildPrehash` ever return whitespace-only?
- **Severity**: MEDIUM
- **Confidence**: 20
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:31-40`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `HmacSignature.Compute` (Core/Auth/SignatureEncoding.cs:41) guards `payload` with `ThrowIfNullOrWhiteSpace`. `BuildPrehash` guards `timestamp`/`method`/`requestPath` with `ThrowIfNullOrWhiteSpace` — so the resulting prehash always contains at least those three non-whitespace segments. No path produces a whitespace-only prehash. Non-issue.
- **Fix**: N/A

### Finding: `queryString` guard is `ThrowIfNull` (not `ThrowIfNullOrWhiteSpace`) — whitespace query passes through as `"?   "`
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:30`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking, confidence 60)
- **Issue**: A caller passing `"   "` (whitespace) for `queryString` will produce a prehash with a `?   ` segment. This is unlikely in practice (the handler would construct the query string from typed values), and the task manifest explicitly states `ThrowIfNull only (may be empty)` — so this is intentional. However, a whitespace-only query string is arguably a caller bug worth rejecting. Given the manifest explicitly specifies this behavior, no change required.
- **Fix**: None required. If a future caller ever accidentally passes whitespace, a `ThrowIfNullOrWhiteSpace` on queryString (raising the guard level from null-only) would catch it earlier — but this is a pre-emptive suggestion, not a defect.
- **Pattern reference**: `OkxSignatureService.cs:39` uses `ThrowIfNull` for `body` by the same logic.

### Finding: `BuildPrehash` is `public static` on an `internal sealed` class — accessible only within the assembly
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:25`
- **Category**: Style
- **Verdict**: PASS
- **Issue**: `public static` on a member of an `internal` type is accessible only within the assembly and test assemblies via `InternalsVisibleTo`. The OKX counterpart follows the same pattern (`OkxSignatureService.cs:34`). `public` is semantically correct here — it makes the member visible to test assemblies without an `InternalsVisibleTo` per method. Consistent with the codebase.
- **Fix**: N/A

### Finding: `BitgetSigningRequest` is a verbatim port of `OkxSigningRequest` with only the key string changed
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs:7`
- **Category**: Code Quality
- **Verdict**: PASS
- **Issue**: The implementation is a precise structural mirror of `OkxSigningRequest.cs`. The key string `"bitget.signed"` is distinct from `"okx.signed"`, so there is no collision. `IsSigned` return expression `TryGetValue(...) && v` is idempotent across retries (Options.Set(true) is a no-op from the second call, TryGetValue returns true). All guards present. This duplication is the accepted per-exchange pattern per ADR-001 conv 1.
- **Fix**: N/A

### Finding: `FormatTimestamp` uses epoch-milliseconds (`ToUnixTimeMilliseconds`) with `InvariantCulture`
- **Severity**: HIGH (acceptance criteria item)
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:38-39`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: Correctly uses `ToUnixTimeMilliseconds()` (not seconds) and `CultureInfo.InvariantCulture`. This is the Bitget-specific delta vs OKX ISO-8601. Matches the task spec.
- **Fix**: N/A

### Finding: `Sign` returns base64 (not hex, not appended); delegates to `HmacSignature.Compute` with `SignatureEncoding.Base64`
- **Severity**: HIGH (acceptance criteria item)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:22-23`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: No crypto re-implemented. `HmacSignature.Compute(_secretKey, payload, SignatureEncoding.Base64)` is the single primitive. Return value is pure base64 — not appended to query or request path.
- **Fix**: N/A

### Finding: `InitializeSecretKey` mirrors OKX/Binance secret guard
- **Severity**: HIGH (acceptance criteria item)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:47-50`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `ArgumentException.ThrowIfNullOrWhiteSpace(secretKey)` at construction time — identical to `OkxSignatureService.cs:52-55` and `BinanceSignatureService.cs:22-24`. Guard fires at `new BitgetSignatureService(...)` before any signing occurs.
- **Fix**: N/A

---

## Summary

- PASS: `Sign` delegation to `HmacSignature.Compute` with `SignatureEncoding.Base64` — no re-implemented crypto, returns base64 only.
- PASS: Prehash assembly — `timestamp + UPPER(method) + requestPath + ('?' + query when non-empty) + body`. Guards correct per spec: `ThrowIfNullOrWhiteSpace` on identity fields, `ThrowIfNull` on optional-empty fields.
- PASS: Timestamp — `ToUnixTimeMilliseconds().ToString(InvariantCulture)`. Correctly milliseconds, correctly culture-invariant.
- PASS: `InitializeSecretKey` guard mirrors OKX and Binance patterns exactly.
- PASS: `BitgetSigningRequest.IsSigned`/`MarkSigned` — idempotent, `null`-guarded, key collision-free.
- PASS: `sealed`/`internal` types, primary constructor, no public mutable fields.
- PASS: Build clean — 0 warnings, 0 errors with `TreatWarningsAsErrors=true`.
- PASS: Lean docs — `<inheritdoc />` on `Sign`, concise `<summary>` on helpers. ADR-001 conv 7 compliant.
- CONCERN: Whitespace-only `queryString` passes `ThrowIfNull` and becomes `"?   "` in the prehash (confidence: 60/100, non-blocking). Intentional per manifest spec.

## Final Verdict

**APPROVED — 0 blocking items.**
