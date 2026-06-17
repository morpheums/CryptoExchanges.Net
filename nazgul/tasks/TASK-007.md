---
id: TASK-007
status: DONE
---

# TASK-007: BybitErrorTranslator + BybitTimeSync

**Milestone**: M-BYBIT
**Wave**: 4
**Group**: 4
**Status**: PLANNED
**Depends on**: TASK-005
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (typed error surface + clock-skew handling)
**Blast radius**: LOW — new files in Bybit project; implements Core `IExchangeErrorTranslator`.

## Description
Implement `BybitErrorTranslator : IExchangeErrorTranslator` mapping Bybit's `retCode`/`retMsg` envelope (and HTTP 401/403/429) to the Core typed exception hierarchy (`AuthenticationException`, `RateLimitExceededException`, `InvalidOrderException`, `InsufficientBalanceException`, `ExchangeApiException`). Implement `BybitTimeSync` to compute the clock-skew offset from Bybit's server-time endpoint (writes into the shared `long[]` offset holder, same pattern as Binance). Use `RetryAfterReader` for rate-limit `RetryAfter`.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs
- src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceErrorTranslator.cs:19-24` ({code,msg} → typed exceptions, auth/rate-limit mapping)
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs` (offset computation into long[] holder)
- `src/CryptoExchanges.Net.Http/RetryAfterReader.cs`

## Acceptance Criteria
1. Bybit auth-failure ret codes and HTTP 401/403 map to `AuthenticationException`; 429/rate-limit codes map to `RateLimitExceededException` with `RetryAfter` populated from headers.
2. Unknown error codes map to `ExchangeApiException` carrying `retCode`/`retMsg`; success envelopes (`retCode == 0`) are NOT treated as errors.
3. `BybitTimeSync` computes a signed offset (server − local) and writes it via `Interlocked` into the shared holder.

## Test Requirements
- Unit tests (TASK-008) cover each error-code → exception-type mapping and the offset sign/magnitude from a stubbed server-time response.

## Implementation Notes

### Files created
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs`

### BybitErrorTranslator
`public sealed class BybitErrorTranslator : IExchangeErrorTranslator` — mirrors the structural
shape of `BinanceErrorTranslator` exactly: same `Translate(HttpResponseMessage, string)` signature,
same `Parse(body)` private helper, same ordered if-cascade returning typed exceptions, and the same
`using` set (`System.Net`, `Core.Exceptions`, `Http`; `Core.Interfaces` + `System.Text.Json` come
from GlobalUsings).

Visibility: `public` to match `BinanceErrorTranslator`. `IExchangeErrorTranslator` lives in Core and
the translator is resolved across assemblies (DI / resilience pipeline), so it cannot be `internal`
even though Bybit's signing infra is internal. This is the documented exception to the "prefer
internal" posture for Bybit.

Envelope parsing: reads `retCode` (Number) and `retMsg` (String) instead of Binance's `code`/`msg`.
Non-JSON / empty bodies yield `(null, null)` and fall through to `ExchangeApiException` — identical
to Binance. The success envelope (`retCode == 0`) is guarded and never treated as a business error.

retCode → exception mapping table (ordered as evaluated):

| Condition                                              | Exception                       | Notes |
|--------------------------------------------------------|---------------------------------|-------|
| `retCode == 0`                                         | (pass-through; not an error)    | Defensive guard; success envelope |
| HTTP 429, or retCode 10006, 10018                      | RateLimitExceededException      | 10006 = too many visits (IP limit); 10018 = exceeded IP rate limit. `RetryAfter` from headers via `RetryAfterReader.GetDelay` |
| HTTP 401/403, or retCode 10003, 10004, 10005, 10010, 33004 | AuthenticationException     | 10003 invalid key; 10004 invalid signature; 10005 permission denied; 10010 unmatched/disallowed IP; 33004 API key expired |
| retCode 110007, 170131                                 | InsufficientBalanceException    | 110007 insufficient available balance; 170131 insufficient balance (spot) |
| retCode 110001, 110003, 110004, 170135, 170136, 170140 | InvalidOrderException          | 110001 order does not exist; 110003 price out of range; 110004 qty/wallet issue; 170135/170136 price/qty precision/filter; 170140 order value below minimum |
| any other retCode (incl. `null`)                       | ExchangeApiException            | Carries retCode + raw body for diagnostics |

Conservative mappings: the Bybit V5 spot order-validation space spans a large 170xxx family. Rather
than over-claim coverage, only the common, well-known order-error members are mapped to
`InvalidOrderException`; all other 170xxx (and any unrecognized retCode) fall through to
`ExchangeApiException`, which preserves the retCode and body. A comment in the source documents this
choice.

### BybitTimeSync
`public static class BybitTimeSync` mirroring `BinanceTimeSync`. Keeps the pure
`ComputeOffset(serverTimeMs, localNowMs) => serverTimeMs - localNowMs` (signed, server − local) so
TASK-008 can assert offset sign/magnitude directly. Adds `ApplyOffset(serverTimeMs, localNowMs,
long[] offsetHolder)` which computes the offset and writes it atomically into the shared
single-element holder via `Interlocked.Exchange(ref offsetHolder[0], offset)` — the identical write
pattern used inline by `BinanceExchangeClient.SyncServerTimeAsync` (line 106). Returns the written
offset. Null-guards the holder.

### Deviation from the Binance pattern
- Binance performs the `Interlocked.Exchange` inline in `BinanceExchangeClient.SyncServerTimeAsync`,
  leaving `BinanceTimeSync` as a pure function. TASK-007 explicitly requires `BybitTimeSync` to
  perform the Interlocked write itself, so the write is encapsulated in `BybitTimeSync.ApplyOffset`.
  The pure `ComputeOffset` is retained for parity and unit-testability. Justification: the task
  manifest requirement, and it makes the holder-write independently testable in TASK-008.

### Verification
`dotnet build CryptoExchanges.Net.sln` → Build succeeded. 0 Warning(s), 0 Error(s).

## Rework (review round 1)
- **Blocking** (api-reviewer REJECT@95, corroborated by security): `BybitTimeSync.ApplyOffset` null-checked `offsetHolder` but not length → `new long[0]` would throw `IndexOutOfRangeException` instead of a clean `ArgumentException`. **Fix**: added `if (offsetHolder.Length < 1) throw new ArgumentException(...)` after the null check.
- **Non-blocking deferred**: Finding 2 (scope `ApplyOffset` internal) — manifest already justifies public for TASK-008 testability; api-reviewer marked it deferred. Finding 3 (CS1591 suppression tech-debt). Both non-blocking.
- Build after fix: 0w/0e.

## Commits
- **Commit**: c6bfbb3 feat(M2): TASK-007 BybitErrorTranslator + BybitTimeSync
- **Commit (rework)**: 456a208 fix(M2): TASK-007 add zero-length guard to BybitTimeSync.ApplyOffset

## Review
- **Review Gate**: PASSED (round 2)
- **Reviewers**: architect-reviewer (APPROVE/93), code-reviewer (APPROVE/88), security-reviewer (APPROVE), api-reviewer (round 1 REJECT@95 → round 2 APPROVE@99 after offsetHolder length guard)
- **Pre-checks**: build 0w/0e, tests 135 passed / 0 failed
- **Deferred non-blocking**: scope ApplyOffset internal (manifest justifies public for TASK-008 testability); CS1591 suppression tech-debt.

## Post-merge-review fix (PR #11, GitHub Copilot reviewer)
- **Finding (verified real)**: `BybitErrorTranslator.Parse` called `m.GetString()` on `retMsg` without a ValueKind guard. `JsonElement.GetString()` throws `InvalidOperationException` (NOT `JsonException`) for a non-string retMsg (number/object/array/bool), which would escape the `catch (JsonException)` and crash the resilience pipeline on a malformed Bybit body. The sibling `retCode` parse was already ValueKind-guarded — asymmetry the 4-reviewer board missed.
- **Fix**: guard `m.ValueKind == JsonValueKind.String` before `GetString()` (null/non-string → null msg, already handled downstream). Added 3 regression theory cases (retMsg as number/object/null → no throw, still classified by retCode). Bybit unit tests 77 → 80; full build 0w/0e, all pass.
- **Commit**: (see fix commit below)
