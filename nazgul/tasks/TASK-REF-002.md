---
id: TASK-REF-002
status: PLANNED
---

# TASK-REF-002: Interface seams for time-sync + signing (DIP, before Bitget)

**Status**: PLANNED (architect-recommended 2026-06-18; maintainer mandate — interfaces over static for swappable behavior)
**Depends on**: PR #13 (TASK-REF-001) MERGED to main — this builds on #13's `ExchangeTimeSync` + shared DI helper.
**Blast radius**: MEDIUM — Core + Http + all 3 exchanges; behavior-preserving. architect + api review.
**Own PR** (refactor, before M-BITGET / TASK-016) per the per-concern PR strategy.

## Scope (exactly this — architect ruling; do NOT interface-ify everything)
1. **`IExchangeTimeSync`** (Core.Resilience): 2 methods (`ComputeOffset`, `ApplyOffset`). `ExchangeTimeSync` becomes the instance impl (`: IExchangeTimeSync`). Register once, non-keyed: `TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()` in `ExchangeServiceRegistration` (exchange-agnostic). Thread through the 3 `XxxExchangeClient` ctors + 3 composers (`ComposeWith`/`ComposeOver`/`ComposeForDi` resolve/pass it; factory-free `Create` passes `new ExchangeTimeSync()`). ~10 files, mechanical.
2. **`ISignatureService`** (Core.Auth): `string Sign(string payload)`. `BinanceSignatureService` / `BybitSignatureService` / `OkxSignatureService` implement it; each `XxxSigningHandler` ctor takes `ISignatureService` instead of the concrete type. No DI registration (composer-constructed). ~3 files/exchange.
3. **KEEP STATIC** (do NOT touch): `HmacSignature` + `SignatureEncoding` (pure crypto primitive), `XxxClientComposer`, `ExchangeServiceRegistration`, `ServiceCollectionExtensions`, pipeline builders.
4. Lean comments throughout: docs on the new interfaces; `<inheritdoc />` on impls; no noise (ADR-001 conv 7).

## Acceptance
1. `ExchangeTimeSync` is injected via `IExchangeTimeSync`; no static call sites remain in the clients. A consumer can override the DI registration to swap it.
2. The 3 signature services share `ISignatureService`; handlers depend on the interface.
3. Behavior byte-identical; full build clean (TreatWarningsAsErrors); ALL tests pass; no Binance/Bybit/OKX regression.
4. Bitget (TASK-016+) uses both interface seams from the start.

## Notes
- Gated on #13 merge → branch off main → TASK-REF-002 → PR/merge → then M-BITGET.
- Retro comment sweep of existing code tracked separately as GitHub issue #14 (going-forward enforcement is already live via the reviewer agents + ADR-001).
