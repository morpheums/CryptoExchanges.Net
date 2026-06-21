---
verdict: APPROVE
---
# Code Review — TASK-057 (Re-review, retry 1/3)

## What Was Reviewed

Re-review covers two post-approval changes:
1. **DIP fix**: `IKucoinSignatureService` interface introduced; `KucoinSigningHandler` now depends on the interface rather than the concrete type.
2. **Simplify pass** (squash commit `4799140`): `/// <inheritdoc />` applied to `SignPassphrase` impl; `using System.Globalization` added; `.ToUniversalTime()` no-op removed from `FormatTimestamp`; `"2"` extracted to `private const string KeyVersion`.

Build: `dotnet build CryptoExchanges.Net.sln` — 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).
Tests: `dotnet test tests/CryptoExchanges.Net.Kucoin.Tests.Unit/` — 44/44 passed.

---

## Findings

### Finding: `Sign` impl uses `<inheritdoc />` but base `ISignatureService.Sign` has no guard; the impl delegates to `HmacSignature.Compute` which guards — no regression
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:17-19`
- **Category**: Correctness
- **Verdict**: PASS (non-blocking)
- **Issue**: `Sign` carries `<inheritdoc />` and does not call `ArgumentException.ThrowIfNullOrWhiteSpace` itself. However, the entire method body is `HmacSignature.Compute(...)`, which guards both `secret` and `payload` with `ThrowIfNullOrWhiteSpace` (confirmed: `src/CryptoExchanges.Net.Core/Auth/HmacSignature.cs:27-28`). The guard is therefore still exercised — it just lives one frame deeper. LR-001 is satisfied by delegation to the Core primitive. Non-blocking.
- **Fix**: N/A — guard is present via delegation.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/HmacSignature.cs:27-28`

### Finding: Missing blank line between `KeyVersion` const and `/// <inheritdoc />`
- **Severity**: LOW
- **Confidence**: 45
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:24-25`
- **Category**: Style
- **Verdict**: PASS (non-blocking, confidence < 80)
- **Issue**: `private const string KeyVersion = "2";` and `/// <inheritdoc />` sit on consecutive lines with no blank line separator. C# convention and Roslyn IDE0055 both expect a blank line between a field declaration and a following member. Does not affect correctness or compilation (no warning because it is a const, not an instance member). Cosmetic only.
- **Fix**: Insert one blank line between `private const string KeyVersion = "2";` and `/// <inheritdoc />`. Non-blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:15-17` (blank line separates field from first method)

---

## Re-review Checklist Against Prior Non-Blocking Notes

### Prior concern: `KucoinSigningHandler` took the concrete type, not an interface — addressed
**Status: RESOLVED.** `IKucoinSignatureService` is now in its own file (`src/CryptoExchanges.Net.Kucoin/Auth/IKucoinSignatureService.cs`), `KucoinSigningHandler` constructor now depends on `IKucoinSignatureService`. One-type-per-file convention holds. The interface inherits `ISignatureService` and adds `SignPassphrase`, which is the correct design for the passphrase-v2 requirement.

### Prior concern: `SignPassphrase` had duplicate `<summary>` on the impl — addressed
**Status: RESOLVED.** `SignPassphrase` on `KucoinSignatureService` now uses `/// <inheritdoc />`. Doc lives on the interface only.

### Prior concern: `"2"` magic literal in header write — addressed
**Status: RESOLVED.** `private const string KeyVersion = "2"` extracted and used correctly on line 83 (`request.Headers.Add("KC-API-KEY-VERSION", KeyVersion)`). No remaining magic literal.

### Prior concern: `.ToUniversalTime()` no-op on `DateTimeOffset.UtcNow` — addressed
**Status: RESOLVED.** Removed. `DateTimeOffset.UtcNow.AddMilliseconds(timeOffset()).ToUnixTimeMilliseconds()` is already UTC-safe — `ToUnixTimeMilliseconds()` uses the UTC representation internally. The `FormatTimestamp_ConvertsToUtc` test confirms correctness for non-UTC offsets.

---

## Convention Checklist

- [x] One type per file: PASS — `IKucoinSignatureService` in its own file; all five production files each contain exactly one top-level type
- [x] `internal sealed` / `internal interface`: PASS — all production types are `internal`; `KucoinSignatureService` and `KucoinErrorTranslator` are `sealed`
- [x] XML docs: PASS — `<summary>/<param>/<returns>/<exception>` on the interface; `<inheritdoc />` on all impl overrides; full docs on `BuildPrehash` and `FormatTimestamp` (static helpers, not interface members)
- [x] Argument guards (LR-001): PASS — `ThrowIfNullOrWhiteSpace` on `passphrase` (SignPassphrase), `timestamp`/`method`/`requestPath` (BuildPrehash), `secretKey` (InitializeSecretKey); `ThrowIfNull` on `body`/`request`/`response`; runtime `string.IsNullOrEmpty` guards on `apiKey`/`passphrase` in `ResignAsync` are intentional lazy-validation for the misconfiguration path
- [x] `.ConfigureAwait(false)`: PASS — both `await` calls in `SendAsync` and `ResignAsync` carry `.ConfigureAwait(false)`
- [x] CancellationToken forwarded: PASS — `ct` forwarded to `ReadAsStringAsync` and `base.SendAsync`
- [x] JSON ValueKind guards: PASS — `ReadString` in `KucoinErrorTranslator` checks `v.ValueKind == JsonValueKind.String` before calling `v.GetString()`; `JsonException` catch wraps parse; `InvalidOperationException` cannot escape
- [x] Test coverage (LR-005): PASS — 44 tests, all pass; `SignPassphrase` golden-vector test, blank-passphrase rejection test, and `Handler_SignedRequest_PassphraseHeaderIsSignedNotRaw` cover the new interface method end-to-end
- [x] AwesomeAssertions: PASS — `using AwesomeAssertions;` throughout; no FluentAssertions reference
- [x] LEAN comments: PASS — no banner essays, no code-restating inline comments; comments explain non-obvious exchange quirks (prehash byte-for-byte consistency, passphrase-v2 rationale, mark-and-strip behavior, timestamp format divergence from OKX)

---

## Summary

All prior non-blocking notes from the original review were addressed correctly:

- PASS: DIP fix — `IKucoinSignatureService` in its own file, `KucoinSigningHandler` depends on the interface
- PASS: `<inheritdoc />` on `SignPassphrase` impl — doc duplication eliminated
- PASS: `KeyVersion` const — `"2"` extracted, used consistently in the single header write
- PASS: `.ToUniversalTime()` removed — `FormatTimestamp` is correct and the UTC-offset test confirms it
- PASS: Build — 0 warnings, 0 errors
- PASS: Tests — 44/44

No new issues introduced. One cosmetic LOW/non-blocking note (missing blank line between `KeyVersion` const and `/// <inheritdoc />`).

## Final Verdict

**APPROVED**
