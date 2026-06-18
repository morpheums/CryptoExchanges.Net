# API Review: TASK-011 — OkxSignatureService + OkxSigningRequest

**Reviewer**: api-reviewer
**Task**: TASK-011
**Date**: 2026-06-18
**Files reviewed**:
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`

---

## Scope Confirmation

Both files are **new additions only**. No existing interfaces in `src/CryptoExchanges.Net.Core/Interfaces/`, no existing models, enums, or public exchange entry types were modified. No breaking change risk from this diff.

---

## Findings

### Finding: OkxSigningRequest is internal vs BybitSigningRequest which is public
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs:5`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `BybitSigningRequest` is `public static class` (an accidental escape of an internal type, pre-ADR-001). `OkxSigningRequest` is correctly `internal static class` per ADR-001 convention #2. This is a positive divergence — OKX is correct; Bybit was wrong and remains public for legacy reasons.
- **Fix**: No fix needed for OKX. The Bybit public class is a known debt tracked under ADR-001 (harmonize to internal during TASK-009 or later). The review gate should note that Bitget must also use `internal` when its signing request is implemented.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:5` (the public legacy), ADR-001 convention #2.

---

### Finding: No unintended public API surface leaks through the internal class
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs:10,17`; `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:25,44,59`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: Members `MarkSigned`, `IsSigned`, `Sign`, `BuildPrehash`, and `FormatTimestamp` are declared `public` — but the enclosing types are `internal sealed class` and `internal static class`. The effective visibility cap is `internal`. No type or member appears in the assembly's public API surface.
- **Fix**: None required.
- **Pattern reference**: ADR-001 convention #2 — "only `XxxExchangeClient` + `XxxOptions` are public per exchange."

---

### Finding: XML docs — no dangling see cref to not-yet-existing OkxSigningHandler
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:12-15`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: The `<remarks>` on `OkxSignatureService` mentions "the OKX signing handler (added in a later task)" as plain prose, not a `<see cref="OkxSigningHandler"/>`. If a `<see cref>` to a not-yet-existing type were present it would produce CS1574 under `TreatWarningsAsErrors`.
- **Fix**: None required. Implementation notes explicitly called out this constraint and it was correctly honoured.
- **Pattern reference**: TASK-011.md implementation notes: "plain text — no `<see cref>` to the not-yet-existing OkxSigningHandler to avoid CS1574."

---

### Finding: see cref references inside OkxSignatureService all resolve
- **Severity**: MEDIUM
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:23,33,54`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: Three `<see cref>` usages present: `BuildPrehash` (static method on same class — resolves), `FormatTimestamp` (static method on same class — resolves), `DateTimeOffset` (BCL type — resolves). No CS1574 risk.
- **Fix**: None required.

---

### Finding: Naming consistency across signing family (Sign/BuildPrehash/FormatTimestamp/MarkSigned/IsSigned)
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`; `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `Sign` matches `BybitSignatureService.Sign`. `MarkSigned`/`IsSigned` match `BybitSigningRequest.MarkSigned`/`IsSigned`. `BuildPrehash` replaces Bybit's `BuildGetSignString`/`BuildPostSignString` (OKX uses a unified builder, which is correct since OKX's prehash format is symmetric for GET/POST with `body=""` for GET). `FormatTimestamp` is a new helper with no Bybit counterpart (Bybit uses epoch-ms, OKX uses ISO-8601 — the helper is appropriate and won't be needed for Bitget if Bitget also uses epoch-ms). The family is coherent for future Bitget reuse.
- **Fix**: None required. When Bitget is implemented, if it uses ISO-8601 timestamps the `FormatTimestamp` static should be promoted to `HmacSignature` or a shared `SignatureHelpers` in Core. Flag as low-priority pre-Bitget design consideration.

---

### Finding: Secret key stored as string (not byte[]) — correct delegation to HmacSignature.Compute
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:18`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `BybitSignatureService` stores `private readonly byte[] _secretKeyBytes` because it hand-rolls `HMACSHA256.HashData`. `OkxSignatureService` stores `private readonly string _secretKey` because it delegates to `HmacSignature.Compute(string secret, ...)` which performs the UTF-8 encoding internally. The storage type difference is correct and intentional.
- **Fix**: None required.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:43` — `Encoding.UTF8.GetBytes(secret)` is inside `HmacSignature.Compute`.

---

### Finding: body guard uses ThrowIfNull (not ThrowIfNullOrWhiteSpace) — correct for GET/DELETE empty body
- **Severity**: MEDIUM
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:49`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `body` is guarded with `ArgumentNullException.ThrowIfNull(body)` (allows empty string) while `timestamp`, `method`, and `requestPath` are guarded with `ThrowIfNullOrWhiteSpace` (rejects empty/whitespace). This matches the OKX spec: GET/DELETE requests have `body = ""`. Rejecting empty body with `ThrowIfNullOrWhiteSpace` would break GET signing.
- **Fix**: None required. The guard asymmetry is deliberate and correct.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:42` — identical `ArgumentNullException.ThrowIfNull(queryString)` for the Bybit GET sign-string `queryString` (also allowed to be empty).

---

### Finding: HmacSignature.Compute payload guard vs empty prehash — theoretical edge case
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:26`; `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:41`
- **Category**: API Design
- **Verdict**: CONCERN
- **Issue**: `HmacSignature.Compute` guards `payload` with `ThrowIfNullOrWhiteSpace` (line 41 of SignatureEncoding.cs). `OkxSignatureService.Sign` passes the prehash directly as `payload`. If a caller somehow passed a prehash that was empty or whitespace-only, `Sign` would throw `ArgumentException` from inside `HmacSignature.Compute` rather than from `Sign` itself, making the stack trace slightly indirect. In practice, the prehash always contains the timestamp (guarded non-empty in `BuildPrehash`), so this path is unreachable through normal use. The risk is only if `Sign` is called with a hand-crafted empty prehash in tests.
- **Fix**: Optionally add `ArgumentException.ThrowIfNullOrWhiteSpace(prehash)` at the top of `Sign` to give a cleaner stack trace. Low priority — the guard in `HmacSignature.Compute` still fires; the error surface is correct.
- **Pattern reference**: ADR-001 convention #4 — "ThrowIfNullOrWhiteSpace / ThrowIfNull at every public/internal boundary."

---

### Finding: InternalsVisibleTo entries in OKX csproj are justified
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:19-22`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: The OKX project grants `InternalsVisibleTo` to `CryptoExchanges.Net.Okx.Tests.Unit`, `CryptoExchanges.Net.Okx.Tests.Integration`, and `DynamicProxyGenAssembly2` (NSubstitute/Castle). These are appropriate: the two test assemblies need access to internal signing, DTOs, and services (including `OkxSignatureService` for TASK-015). No consumer application projects are granted visibility.
- **Fix**: None required. Pattern is identical to Bybit's csproj entries.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:19-22`.

---

### Finding: OkxSignatureService.BuildPrehash is static — correctly testable by TASK-015 without constructing the service
- **Severity**: LOW
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:44`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `BuildPrehash` and `FormatTimestamp` are `static`, allowing TASK-015 unit tests to assert prehash assembly and timestamp formatting without needing a valid `secretKey`. This matches the TASK-011 design note: "Static so TASK-015 can unit-test assembly independently of the secret."
- **Fix**: None required.

---

## Summary

- PASS: Visibility (`internal` class) — both types correctly follow ADR-001 conv #2; no public API surface added.
- PASS: Naming consistency — `Sign`/`MarkSigned`/`IsSigned` mirror the Bybit family; `BuildPrehash`/`FormatTimestamp` are correctly differentiated.
- PASS: No dangling `<see cref>` — plain-text prose for the not-yet-existing handler avoids CS1574.
- PASS: All `<see cref>` that exist resolve (`BuildPrehash`, `FormatTimestamp`, `DateTimeOffset`).
- PASS: `body` guard is `ThrowIfNull` (not `ThrowIfNullOrWhiteSpace`) — correct for empty-body GET/DELETE.
- PASS: Secret key stored as `string` (correct delegation to `HmacSignature.Compute`).
- PASS: `InternalsVisibleTo` entries are justified (test assemblies and NSubstitute proxy only).
- PASS: `BuildPrehash`/`FormatTimestamp` are static — correctly testable by TASK-015.
- CONCERN: `Sign` does not guard `prehash` with `ThrowIfNullOrWhiteSpace` before passing to `HmacSignature.Compute` — indirection in stack trace, unreachable in normal use (confidence: 60/100, non-blocking).
- NOTE: `BybitSigningRequest` is `public` (legacy); `OkxSigningRequest` is `internal` (correct). Bitget must also use `internal` when implemented.

---

## Final Verdict

**APPROVED**

Both files are internally-scoped, correctly delegating to the Core HMAC primitive, with no public API surface changes, no dangling cref references, no naming drift, and InternalsVisibleTo entries limited to test assemblies. The single CONCERN (missing prehash guard in `Sign`) is non-blocking at confidence 60 and unreachable through the intended calling path.
