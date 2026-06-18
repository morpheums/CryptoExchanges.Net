---
id: TASK-023
status: IN_PROGRESS
---

> **PR #16 self-review remediation (automated `/code-review`, 7 findings).** User chose full scope (all 7, incl. cross-cutting). Triage:
> - #2 (robustness, REAL): BitgetMarketDataService order-book `b[0]/b[1]` unguarded → bounds guard + test. Matches LR-004.
> - #3 (robustness): DECLINE — siblings (Binance/Bybit/OKX) all throw on unknown side/type/TIF; OrderSide/OrderType/TimeInForce have no `Unknown` member, so a lenient fallback would silently mislabel an order (sell→Buy). Leave thread open w/ rationale.
> - #4 (cleanup): remove dead `BitgetValueParsers.ParseOptionalDecimal` + its unit test (priceAvg never mapped, StopPrice Ignore()'d).
> - #7 (convention): trim `<remarks>`/`<para>` essays on internal BitgetTradingService + BitgetHttpClient (ADR-001 #7).
> - #1 (cross-cutting): per-client `if (serverTimeMs > 0)` skip-guard before ApplyOffset in Binance/Bybit/OKX/Bitget SyncServerTimeAsync; keep Core throw as defense-in-depth.
> - #5 (cross-cutting): promote NormalizeHostRoot → shared Http; adopt in OKX (+ Binance/Bybit/Bitget via shared path).
> - #6 (cross-cutting, signature-sensitive): extract byte-identical BuildQueryString → shared Http helper; rely on per-exchange signing tests.

# TASK-023: PR #16 self-review remediation (7 findings)

**Milestone**: M-BITGET (post-merge polish)
**Status**: READY
**Blast radius**: HIGH — touches shared Http (query builder + host normalization) and all 4 exchange clients (time-sync). Signing-sensitive → full review gate.

## Plan
- Group A (Bitget-local, low risk): #2, #4, #7 (+ decline #3). Build+test+commit.
- Group B (cross-cutting): #1, #5, #6. Build+test+commit.
- Review gate (architect/code/security/api), push, reply+resolve threads (#3 stays open).
