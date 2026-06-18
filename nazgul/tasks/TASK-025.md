---
id: TASK-025
status: IMPLEMENTED
commit: 027d8f4
claimed_at: 2026-06-19
---

# TASK-025: Rename per-exchange wire DTOs — drop redundant exchange prefix, add `Dto` suffix

**Status**: IMPLEMENTED

> **Implementation note (2026-06-19):** Build 0W/0E (Release). Full suite green: 455 tests (438 non-integration + 17 integration), 0 failed. 47 internal wire DTOs renamed across Binance (12), Bybit (13), OKX (11), Bitget (11) via `git mv` + type/reference updates; mapping-profile and symbol-format CLASS names left untouched. Commit 027d8f4.

**Blast radius**: LOW — pure rename of `internal` wire DTO types within each exchange
assembly. No public API change (DTOs are internal), no behavior change. The 455-test
suite is the regression net and must stay green; build must remain 0W/0E.

## Scope
Folds into PR #17 (branch `chore/cleanup-file-per-type`) as a third commit. The exchange
prefix on wire DTOs (`OkxTicker`, `BybitResponse`, …) is redundant with the namespace; the
bare domain names (`Ticker`/`Order`/`Trade`/`OrderBook`/`RateLimit`/`SymbolInfo`) collide
with the Core models they map into. Resolution: rename `<Exchange><Name>` → `<Name>Dto`
(strip exchange prefix, append `Dto`; internal words like `Response`/`Result`/`Ack` left
intact). Applied uniformly to all 47 DTOs across Binance/Bybit/Okx/Bitget, including their
files (`git mv`), mapping profiles, and any test references.

## Acceptance
- Build 0W/0E (Release); all 455 tests pass.
- No `internal` DTO type retains an exchange prefix; every wire DTO ends in `Dto`.
- No collisions with `Core.Models.*`; no public API surface change.

## Commits
- 027d8f4 — refactor(cleanup): rename per-exchange wire DTOs to <Name>Dto (TASK-025)
