# Consolidated Review Feedback — TASK-REF-002

**Aggregate verdict: CHANGES_REQUESTED**
**Round:** 1
**Gate policy:** require_all_approve=true; confidence_threshold=80; block_on_security_reject=true

## Reviewer board

| Reviewer | Verdict | Confidence (blocking finding) |
|---|---|---|
| architect-reviewer | CHANGES_REQUESTED | 95 (MEDIUM) |
| code-reviewer | CHANGES_REQUESTED | 97 (MEDIUM) |
| security-reviewer | APPROVED | — (same item noted at 40, non-blocking) |
| api-reviewer | APPROVED | — (static→instance break noted at 95, documented CONCERN) |

Pre-checks (orchestrator-verified): build 0W/0E (TreatWarningsAsErrors); non-integration tests 335 pass / 0 fail.

## Blocking findings (must fix — confidence ≥80, severity ≥MEDIUM)

### B1 — Remove dead `BinanceSignatureService.BuildSignedQuery` [AUTO-FIX]
- **File:** `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:22-32`
- **Raised by:** architect-reviewer (REJECT 95/MEDIUM), code-reviewer (REJECT 97/MEDIUM)
- **Issue:** Before this refactor, `BinanceSigningHandler` called `signatureService.BuildSignedQuery(...)` on the concrete service. The refactor typed the handler ctor to `ISignatureService` (which does not expose `BuildSignedQuery`) and inlined a byte-identical private `BuildSignedQuery` helper into the handler (`BinanceSigningHandler.cs:84-89`, called at lines 42/76). The service-side `BuildSignedQuery` (lines 22-32, including its full `/// <summary>/<param>/<returns>` doc block) now has **zero callers** anywhere in `src/` or `tests/` — confirmed by grep. It is `public` on an `internal sealed` type, so no external surface, but it is unreachable dead code that:
  1. duplicates logic already correctly living in the handler's private helper (silent-divergence risk if either copy is edited),
  2. retains a verbose doc block on a dead method, violating the lean-comment mandate (ADR-001 conv 7),
  3. misleads future readers into thinking the method is used.
  The manifest's "Extra members left as-is per scope: Binance `BuildSignedQuery` (kept)" is a deferral, not a justification.
- **Fix:** Delete `BinanceSignatureService.BuildSignedQuery` and its doc block (lines 22-32). No behavior change (zero callers); build stays 0W/0E; all 335 tests stay green. The interface-inlined private helper in `BinanceSigningHandler` is the correct and sole location for this logic.
- **Classification:** AUTO-FIX (mechanical deletion of unreachable code; behavior-neutral; no public-surface impact since the type is internal).

## Non-blocking concerns (carry-forward — do NOT gate)

- **C1 (api, 95, intended):** `ExchangeTimeSync` static→instance is a source/binary breaking change for any caller of the former static `ExchangeTimeSync.ComputeOffset/ApplyOffset`. Intended, architect-mandated, pre-1.0. No static call sites remain. Add a CHANGELOG entry before NuGet publish.
- **C2 (api, low):** `ISignatureService.Sign` lacks `<returns>`/`<exception>` XML doc tags. Add before publish.
- **C3 (code, 70):** `ExchangeTimeSync.cs:3` uses class-level `/// <inheritdoc cref="IExchangeTimeSync" />` — valid but the only class-level inheritdoc in the codebase. Optional: replace with a one-line `<summary>`.
- **C4 (security, 40):** same as B1, framed as pre-existing cleanup; subsumed by the B1 fix.

## Confirmed PASS (no action)

- Behavior byte-identical: Binance inlined `BuildSignedQuery` preserves the empty-query separator guard (`q + (q=="" ? "" : "&") + "signature=" + Sign(q)`); `_timeSync.ApplyOffset(...)` identical to former static call (same args, same `Interlocked.Exchange`, same offsetHolder threading).
- `IExchangeTimeSync` registered exactly once, non-keyed, in Http's `ExchangeServiceRegistration.AddExchange` via `TryAddSingleton` (consumer-overridable; verified by `Consumer_Can_Override_ExchangeTimeSync`).
- No over-conversion: `HmacSignature`, `SignatureEncoding`, 3 `XxxClientComposer`, `ExchangeServiceRegistration`, `ServiceCollectionExtensions`, pipeline builders all correctly kept static.
- Layering intact (interfaces in Core, registration in Http; no Core→Http dep; no .csproj changes).
- Signing path unchanged; secrets remain in the concrete services (`byte[]`/`string`), not exposed via the `Sign(string)`-only interface; no secret logging.
- Signature services stay `internal sealed`; client ctors were already internal (the added `IExchangeTimeSync` param is not consumer-facing).
