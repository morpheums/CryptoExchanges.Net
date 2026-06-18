# Code Review — TASK-011: OkxSignatureService + OkxSigningRequest

**Reviewer**: Code Quality (C# 13 / .NET 10)
**Date**: 2026-06-18
**Files reviewed**:
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`

---

## Build & Test Verification

- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s).**
- Unit tests (excluding integration): **Passed — 196 total (93 Core + 80 Bybit + 12 Http + 11 DI).**

---

## Findings

### Finding 1: Timestamp format string — Z is correctly a literal, not a timezone specifier
- **Severity**: LOW (informational — confirmed correct)
- **Confidence**: 99
- **File**: `OkxSignatureService.cs:60`
- **Category**: Correctness
- **Verdict**: PASS
- **Analysis**: The format string `"yyyy-MM-ddTHH:mm:ss.fffZ"` with `CultureInfo.InvariantCulture` on `DateTimeOffset` produces a trailing literal `Z` character. In .NET, `Z` is **not** a recognized custom `DateTimeOffset`/`DateTime` format specifier — it is treated as a literal verbatim character. Confirmed via live execution: `new DateTimeOffset(2026,6,17,12,0,0,500,TimeSpan.Zero).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)` → `"2026-06-17T12:00:00.500Z"` (exact match). The `.ToUniversalTime()` call before `ToString` is correct insurance: even if the caller passes a non-UTC `DateTimeOffset`, the UTC conversion happens first so the offset is always +00:00 and the literal `Z` is semantically accurate.
- **Pattern reference**: Confirmed against .NET 10 runtime behavior.

### Finding 2: `Sign(prehash)` — no guard on `prehash`, but Core validates; confirmed no validation loss
- **Severity**: LOW (informational — confirmed correct)
- **Confidence**: 95
- **File**: `OkxSignatureService.cs:25-26`
- **Category**: Correctness
- **Verdict**: PASS
- **Analysis**: `Sign(prehash)` has no `ArgumentException.ThrowIfNullOrWhiteSpace(prehash)` guard. This is intentional per the task notes: `HmacSignature.Compute` in Core already calls `ArgumentException.ThrowIfNullOrWhiteSpace(payload)` (verified at `SignatureEncoding.cs:41`). Delegating to Core is the correct approach — adding a redundant guard in the caller would double the work and diverge from the simplifier's pass that removed it. The exception type and message surface identically to a caller. No validation is lost.
- **Pattern reference**: `CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:40-41`

### Finding 3: `BuildPrehash` argument guards — correct guard types per parameter semantics
- **Severity**: LOW (informational — confirmed correct)
- **Confidence**: 99
- **File**: `OkxSignatureService.cs:46-49`
- **Category**: Correctness
- **Verdict**: PASS
- **Analysis**: `timestamp`, `method`, and `requestPath` use `ArgumentException.ThrowIfNullOrWhiteSpace` (empty/whitespace is invalid — a GET path of `"  "` would produce a wrong signature). `body` uses `ArgumentNullException.ThrowIfNull` only — empty body is semantically valid for GET/DELETE requests. Guard types are exactly correct per OKX API semantics and match the Bybit pattern for the `queryString`/`jsonBody` parameters.
- **Pattern reference**: `BybitSignatureService.cs:38-43`

### Finding 4: Prehash assembly order — matches OKX documentation
- **Severity**: LOW (informational — confirmed correct)
- **Confidence**: 95
- **File**: `OkxSignatureService.cs:50`
- **Category**: Correctness
- **Verdict**: PASS
- **Analysis**: `$"{timestamp}{method.ToUpperInvariant()}{requestPath}{body}"` matches the OKX canonical prehash `timestamp + METHOD + requestPath + body`. Method is upper-cased inside the builder via `ToUpperInvariant()`, consistent with the XML doc's contract (`method` param doc says "upper-cased before assembly"). This is the correct approach — the builder accepts any casing and normalizes it, so callers passing `"get"` or `"GET"` both produce identical signatures.

### Finding 5: `OkxSigningRequest` access modifier — `internal` vs Bybit's `public`
- **Severity**: LOW
- **Confidence**: 75
- **File**: `OkxSigningRequest.cs:5`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `OkxSigningRequest` is `internal static`, but the reference `BybitSigningRequest` is `public static`. The task notes explicitly state "mirrors `BybitSigningRequest` exactly" and also notes "internal, per ADR-001 conv #2 + post-009B precedent." This is a deliberate deviation (ADR-001 mandates internal for OKX assembly types since the signing handler is also internal). The access modifier is intentionally `internal` here, consistent with the OkxSignatureService being `internal sealed`. However, the task notes say "mirrors exactly" then immediately says it is `internal` — the divergence is documented. Non-blocking since ADR-001 governs this, but the phrase "mirrors exactly" in the notes is slightly misleading.
- **Fix**: No code change needed. Consider updating the task notes to say "mirrors the shape of BybitSigningRequest (guards, key, Set/TryGetValue pattern) but uses `internal` per ADR-001" to avoid future confusion during review.
- **Pattern reference**: `BybitSigningRequest.cs:5` (public), `OkxSignatureService.cs:16` (internal — same convention within OKX assembly)

### Finding 6: XML documentation — completeness check
- **Severity**: LOW (informational — confirmed complete)
- **Confidence**: 99
- **File**: Both files
- **Category**: Code Quality
- **Verdict**: PASS
- **Analysis**:
  - `OkxSignatureService`: class-level `<summary>` + `<remarks>` ✓; `Sign` has `<summary>`, `<param>`, `<returns>` ✓; `BuildPrehash` has `<summary>`, all four `<param>`, `<returns>`, `<exception cref>` for both exception types ✓; `FormatTimestamp` has `<summary>`, `<param>`, `<returns>` ✓; `InitializeSecretKey` is private — no docs required ✓.
  - `OkxSigningRequest`: class-level `<summary>` ✓; `MarkSigned` has `<summary>` ✓; `IsSigned` has `<summary>` ✓. The docs are terse but sufficient — the class is `internal` so there is no public-facing API surface to document exhaustively. Mirrors `BybitSigningRequest` doc style precisely.
  - Note: Both files are `internal` — CS1591 (missing XML comment for publicly visible type) does not apply. The implementation chose to document anyway for consistency, which is correct behavior for this codebase.

### Finding 7: No re-implemented crypto
- **Severity**: LOW (informational — confirmed correct)
- **Confidence**: 99
- **File**: `OkxSignatureService.cs:25-26`
- **Category**: Correctness
- **Verdict**: PASS
- **Analysis**: `Sign` delegates entirely to `HmacSignature.Compute` from `CryptoExchanges.Net.Core.Auth`. No `HMACSHA256`, `Convert.ToBase64String`, or `Encoding.UTF8.GetBytes` appear in the OKX service. Contrast with `BybitSignatureService.cs:24-26` which still hand-rolls the HMAC. The OKX service correctly uses the Core primitive from TASK-009 as required.

### Finding 8: `OkxSigningRequest` idempotency — Set overwrites, TryGetValue reads
- **Severity**: LOW (informational — confirmed correct)
- **Confidence**: 99
- **File**: `OkxSigningRequest.cs:13, 20`
- **Category**: Correctness
- **Verdict**: PASS
- **Analysis**: `request.Options.Set(SignedKey, true)` is idempotent — calling `MarkSigned` multiple times on the same request (retry scenario) simply overwrites with `true` each time. `TryGetValue` returns `false` for an unset key (not an exception). The `&& v` guard in `IsSigned` correctly handles the case where the key exists but is `false` (not currently possible given `MarkSigned` only sets `true`, but defensive).

### Finding 9: Primary constructor + field initializer pattern
- **Severity**: LOW (informational — confirmed correct)
- **Confidence**: 99
- **File**: `OkxSignatureService.cs:16-18`
- **Category**: Code Style
- **Verdict**: PASS
- **Analysis**: Uses C# 12 primary constructor `(string secretKey)` with `private readonly string _secretKey = InitializeSecretKey(secretKey)`. This is identical to `BybitSignatureService.cs:13-15` (which uses `private readonly byte[] _secretKeyBytes = InitializeSecretKey(secretKey)`). The guard is placed in the `private static string InitializeSecretKey(string secretKey)` initializer, matching the project convention exactly.
- **Pattern reference**: `BybitSignatureService.cs:13-15, 63-67`

### Finding 10: No async code — no ConfigureAwait/CT concerns
- **Severity**: LOW (informational — N/A)
- **Confidence**: 99
- **File**: Both files
- **Category**: Correctness
- **Verdict**: PASS
- **Analysis**: Both files are pure synchronous. No `async`/`await`, no `Task`, no `CancellationToken`. No `.ConfigureAwait(false)` or CT checks apply. Correct — signature computation is CPU-bound and synchronous; the signing handler (TASK-012) will own the async context.

---

## Summary

| # | Item | Verdict | Confidence |
|---|------|---------|------------|
| 1 | Timestamp format string `Z` is literal, not timezone specifier | PASS | 99 |
| 2 | `Sign()` no redundant guard — Core validates, no loss | PASS | 95 |
| 3 | BuildPrehash guard types correct per param semantics | PASS | 99 |
| 4 | Prehash assembly order matches OKX spec | PASS | 95 |
| 5 | `OkxSigningRequest` is `internal` while Bybit's is `public` — deliberate per ADR-001 | CONCERN | 75 (non-blocking) |
| 6 | XML docs complete on all members | PASS | 99 |
| 7 | No re-implemented crypto — delegates to Core HmacSignature | PASS | 99 |
| 8 | `MarkSigned`/`IsSigned` idempotent across retries | PASS | 99 |
| 9 | Primary constructor + guard in initializer — matches Bybit pattern | PASS | 99 |
| 10 | No async code — ConfigureAwait/CT N/A | PASS | 99 |

**No REJECT findings. One CONCERN (confidence 75, non-blocking).**

---

## Final Verdict

**APPROVED**

Both files are correct, clean, and well-documented. The implementation faithfully follows the Bybit/Core reference patterns, uses the TASK-009 Core primitive for HMAC computation, assembles the OKX prehash string correctly, and handles the ISO-8601 UTC timestamp with a verified-literal `Z` suffix. The build succeeds with zero warnings/errors under `TreatWarningsAsErrors=true`, and all 196 unit tests pass. The single CONCERN (internal vs public access modifier) is a documented ADR-001 decision, not a defect.
