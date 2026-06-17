# API Review Round 2: TASK-003 — BybitSigningHandler visibility fix

**Commit under review**: 60b55e3 (fix applied to round-1 REJECTs from commit 283bcf0)
**Reviewer**: API Reviewer
**Date**: 2026-06-17
**Round**: 2 (re-review of round-1 REJECT-1 and REJECT-2)

---

## Verification Checklist

### 1. REJECT-1 — Both types are now `internal sealed class`

**Verified.**

- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:17`: `internal sealed class BybitSigningHandler`
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13`: `internal sealed class BybitSignatureService`

Both types now carry the correct `internal` modifier. The `public` exposure that locked signing internals as committed API is gone.

- **Confidence**: 100
- **Status**: RESOLVED

---

### 2. REJECT-2 — `recvWindow` string ctor param downgrade

**Condition satisfied, downgraded.**

Round-1 stated explicitly: "conditioned on Finding 1 — if type becomes `internal` this downgrades to CONCERN/non-blocking."

`BybitSigningHandler` is now `internal`. The single call site that constructs it is internal pipeline code (the DI composer), which controls formatting. The string form at internal visibility is acceptable. No action required on this item.

- **Confidence**: 100
- **Status**: DOWNGRADED to non-blocking CONCERN — no fix required

---

### 3. Round-1 non-blocking CONCERN (confidence 90) — Missing `<param>` XML docs on BybitSigningHandler ctor params

**Verified — addressed.**

`src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13-16` now has all four `<param>` doc tags:
- `<param name="apiKey">` — line 13
- `<param name="signatureService">` — line 14
- `<param name="recvWindow">` — line 15
- `<param name="timeOffset">` — line 16

- **Confidence**: 100
- **Status**: RESOLVED

---

### 4. Build verification

**Clean.**

```
dotnet build CryptoExchanges.Net.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

All projects — Core, Http, Binance, Bybit, DI, tests, samples — built without errors or warnings.

---

### 5. Accepted context: Binance signing-type asymmetry

Acknowledged. `BinanceSigningHandler` and `BinanceSignatureService` remain `public` as a pre-existing condition. The orchestrator has tracked harmonization to `internal` as a deliberate follow-up under TASK-009. This asymmetry is NOT raised as a blocker on TASK-003.

---

## Summary

| Round-1 Item | Round-1 Verdict | Round-2 Status |
|---|---|---|
| REJECT-1: Both types `public` | REJECT (conf 95) | RESOLVED — both are now `internal sealed class` |
| REJECT-2: `recvWindow` string param (if public) | REJECT (conf 80, conditional) | DOWNGRADED — condition (internal visibility) met; non-blocking |
| CONCERN: Missing `<param>` docs (conf 90) | CONCERN | RESOLVED — all four `<param>` tags present |
| CONCERN: BybitSigningRequest public visibility | CONCERN | Unchanged — deferred, Binance parity, non-blocking |
| CONCERN: Guard asymmetry in BuildGet/PostSignString | CONCERN | Moot at internal visibility, no action needed |

All round-1 PASSes carry forward unchanged.

---

## Final Verdict

**APPROVED**

Both blocking items from round 1 are fully resolved. The fix is correct, minimal, and non-destructive — changing `public` to `internal` on two types that have a pre-existing InternalsVisibleTo model to cover the only legitimate internal consumers. Build is clean at 0 warnings / 0 errors. The conditional REJECT-2 is automatically resolved by REJECT-1's fix. The round-1 non-blocking `<param>` concern was addressed as well.

No further changes requested on TASK-003.
