---
verdict: APPROVE
---
# Architect Review — TASK-057 (Re-review, retry 1/3)

## Verdict
APPROVE

## What Was Checked

1. **DIP finding from prior review resolved** — Verified `KucoinSigningHandler` constructor parameter is `IKucoinSignatureService` (line 21), not the concrete class.
2. **Interface correctness** — `IKucoinSignatureService : ISignatureService`, adds `SignPassphrase(string)`, is `internal`, is in its own file (`Auth/IKucoinSignatureService.cs`).
3. **Call sites in handler** — `signatureService.Sign(prehash)` (line 67) and `signatureService.SignPassphrase(passphrase)` (line 70) both dispatch through the interface, not the concrete type.
4. **Simplify-pass changes** — `KeyVersion = "2"` const, `using System.Globalization`, removed `.ToUniversalTime()` no-op, `<inheritdoc/>` on impl — all reviewed; no regressions.
5. **Layer compliance** — KuCoin assembly `ProjectReference` nodes: Core + Http only; no DeltaMapper or Core.Models references in auth/resilience files.
6. **Handler doc comment** — `<see cref="KucoinSignatureService.SignPassphrase"/>` at line 13 references the concrete class by name in an XML doc, but only inside a `<remarks>` cross-reference. This is a documentation curiosity, not a runtime coupling concern (the field is typed to the interface).
7. **Static helpers on KucoinSignatureService** — `BuildPrehash` and `FormatTimestamp` are `public static` and called directly in `KucoinSigningHandler` (lines 51, 66). This is a static call on the concrete class from the handler — see Finding 1 below.
8. **Build and tests** — `dotnet build` (Release): 0W/0E. All 44 tests pass.

---

## Findings

### Finding 1: Handler calls KucoinSignatureService static methods directly (non-blocking)
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:51,66`
- **Category**: Architecture (DIP — soft concern)
- **Verdict**: CONCERN (non-blocking — confidence 70, severity LOW)
- **Issue**: `KucoinSigningHandler.ResignAsync` calls `KucoinSignatureService.FormatTimestamp(instant)` and `KucoinSignatureService.BuildPrehash(...)` via static dispatch on the concrete class, while the instance field `signatureService` is correctly typed to `IKucoinSignatureService`. These two static helpers are pure, deterministic functions with no I/O side effects and no swappable implementation (they encode the KuCoin wire format, not a behavior the maintainer would inject). The OKX handler does the same (`OkxSignatureService.FormatTimestamp`, `OkxSignatureService.BuildPrehash`). By Rule #11 the exemption for "genuinely fixed pure helpers" covers this use. At confidence 70, this is a non-blocking concern worth flagging if the number of exchanges grows and these helpers are ever duplicated to an Nth copy.
- **Fix (if desired)**: Move `BuildPrehash` and `FormatTimestamp` to the interface as `static abstract` members, or to a standalone `KucoinPrehash` static class. Either removes the concrete coupling from the handler without changing the public surface. Not required to merge.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:48,63` — same pattern in OKX reference; this codebase accepts it.

---

## DIP Finding Resolution Verification

| Check | Result |
|---|---|
| `IKucoinSignatureService` file exists in `Auth/` folder | PASS |
| Interface is `internal` | PASS |
| Interface extends `ISignatureService` | PASS |
| Interface adds `SignPassphrase(string)` | PASS |
| `KucoinSignatureService` implements `IKucoinSignatureService` | PASS |
| `KucoinSigningHandler` constructor parameter type | `IKucoinSignatureService` (interface) — PASS |
| `signatureService.Sign()` dispatches via interface | PASS |
| `signatureService.SignPassphrase()` dispatches via interface | PASS |

---

## Layering Check

- `CryptoExchanges.Net.Kucoin.csproj` `<ProjectReference>`: Core + Http only — CLEAN.
- `KucoinSignatureService`: imports `System.Globalization`, `CryptoExchanges.Net.Core.Auth` only — CLEAN.
- `KucoinSigningHandler`: imports `CryptoExchanges.Net.Core.Auth`, `CryptoExchanges.Net.Kucoin.Auth` — CLEAN (intra-exchange is correct).
- `KucoinErrorTranslator`: imports `System.Globalization`, `System.Net`, `CryptoExchanges.Net.Core.Exceptions`, `CryptoExchanges.Net.Http` — CLEAN (Core.Exceptions + Http is the correct allowed set).
- `KucoinSigningRequest`: no external imports — CLEAN.
- No `Core.Models` reference anywhere in auth/resilience files — K1 invariant CLEAN.
- No DeltaMapper reference in auth/resilience files — CLEAN.

---

## Simplify-Pass Changes (No Regressions)

- `KeyVersion = "2"` const: eliminates the magic string literal; correct.
- `using System.Globalization`: added for `CultureInfo.InvariantCulture` in `FormatTimestamp`; correct.
- Removed `.ToUniversalTime()` no-op: `DateTimeOffset.ToUnixTimeMilliseconds()` is already UTC-normalized; the removal is correct.
- `<inheritdoc/>` on `Sign` and `SignPassphrase` implementations: follows interface XML docs; correct.
- Missing blank line between `private const string KeyVersion = "2";` and `/// <inheritdoc />` (line 24-25): minor style issue, not blocking.

---

## Build & Test

- `dotnet build src/CryptoExchanges.Net.Kucoin/ -c Release`: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true`.
- `dotnet test tests/CryptoExchanges.Net.Kucoin.Tests.Unit/`: **44/44 passed**, 0 failed, 0 skipped.
