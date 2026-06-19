---
id: TASK-031
status: DONE
depends_on: [TASK-029]
commit: 2de728c
claimed_at: 2026-06-19T07:00:00Z
---
# TASK-031: Account tools (read-scoped credentials)

## Metadata
- **ID**: TASK-031
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-029
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs, tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs]
- **Wave**: 3
- **Traces to**: Approved design §Tool surface (Account — read-scoped) + §Credentials (AuthRequired); Approved plan **Task 4**
- **Created at**: 2026-06-19T04:00:00Z
- **Claimed at**: 2026-06-19T07:00:00Z
- **Implemented at**: 2026-06-19T07:05:00Z
- **Completed at**: 2026-06-19T09:00:00Z
- **Blocked at**:
- **Retry count**: 1/3
- **Test failures**: 0

## Status

Blast radius: 1 new tool file + 1 test file. Disjoint from TASK-030 (market-data tools) — the
two run in parallel in Wave 3. No existing source touched.

## Description

Add the 6 read-only account tools (read-scoped credentials). Implement faithfully per
**plan Task 4** (it has the exact `AccountTools.cs` + the failing tests). Follow the TDD order.

`[McpServerToolType] static class AccountTools` — 6 static tools, first param
`IExchangeClientFactory factory` (SDK injection), returning `Task<ToolResult<T>>`, with rich
`[Description]`s:
- `GetBalances`, `GetBalance` (asset string → `Asset.TryOf`; bad asset → error),
  `GetOpenOrders` (symbol optional), `GetOrder` (symbol + orderId),
  `GetOrderHistory` (limit default 500), `GetTradeHistory` (limit default 500).

Account reads come from `IAccountService` (balances, trade history) and `ITradingService`
**read methods only** (`GetOpenOrdersAsync`, `GetOrderAsync`, `GetOrderHistoryAsync`).
**No order placement/cancel** — write methods on `ITradingService` are never exposed. Missing
credentials surface as a structured `AuthRequired` error via `ToolRunner` (the existing
`AuthenticationException` maps there) — tools never throw across the boundary.

NOTE (from plan): confirm the `AssetBalance` constructor arg order
(`(Asset, decimal Free, decimal Locked)` per Core.Models) and adjust the test literal if needed.

## Acceptance Criteria
- [x] `dotnet build CryptoExchanges.Net.sln -c Release` succeeds with **0 warnings / 0 errors** (TreatWarningsAsErrors).
- [x] `AccountToolsTests` pass (balances→data, missing-credentials→AuthRequired, bad-asset→error); existing 455 tests stay green.
- [x] Exactly 6 `[McpServerTool]` methods exist on `AccountTools`, all read-only (no Place/Cancel/Create/Submit/Delete); each has a non-empty `[Description]`.

## Pattern Reference
- Read surfaces being called: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClient.cs`
  (`IAccountService`: GetBalancesAsync / GetBalanceAsync / GetTradeHistoryAsync;
  `ITradingService` reads: GetOpenOrdersAsync / GetOrderAsync / GetOrderHistoryAsync).
- `Asset.TryOf`: `src/CryptoExchanges.Net.Core/Models/Asset.cs`; `AssetBalance`/`Order`/`Trade`:
  `src/CryptoExchanges.Net.Core/Models/Models.cs`.
- AuthRequired mapping established in TASK-029 `ToolRunner`.
- Exact create code: **plan Task 4, Steps 1–5** — implement as written.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — Approved design §Tool surface → Account (6 read-scoped tools), §Credentials (AuthRequired on missing keys)
- **TRD Component**: n/a — `AccountTools`
- **ADR Reference**: ADR-001 (thin facade over existing reads; read-only — no trading writes)

## Implementation Log

### Attempt 1
- **Base SHA**: 36704699930575d375bb5852dc09f928395be89b
- Claimed at: 2026-06-19T07:00:00Z
- Created `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs` — 6 `[McpServerTool]` static methods following MarketDataTools pattern (LR-001 guards, `Run<T>` helper, `Unavailable()` error, `ToolRunner.RunAsync` wrapping)
- Created `tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs` — 7 tests: GetBalances returns data, AuthRequired on missing creds (via mocked AuthenticationException), bad asset returns error, unknown exchange returns ExchangeUnavailable, GetOpenOrders returns data, GetOpenOrders AuthRequired, GetTradeHistory returns data
- Build: 0W/0E. All 476 tests pass (full solution, Category!=Integration filter).
- Commit: c27e976

## Commits
- c27e976: feat(FEAT-002): TASK-031 — read-only account tools (balances/orders/history)
- 2de728c: feat(FEAT-002): TASK-031 remediation — BadRequest bad-asset + GetOrder/GetOrderHistory happy-path tests

### Attempt 2 (remediation)
- **Base SHA**: 2abab6e
- Claimed at: 2026-06-19T08:00:00Z
- Fix 1 (blocking — api/code REJECT): `GetBalance` bad-asset path: replaced `FormatException` throw with `Task.FromResult(ToolResult<AssetBalance>.Failure(new ToolError("BadRequest", ...)))` directly. Asset.TryOf check now precedes exchange resolution, keeps structured-error-not-exception MCP boundary design.
- Fix 2 (blocking — architect/code/api REJECT, LR-005): Added `GetBalance_ReturnsData` happy-path test (mocks `GetBalanceAsync`, asserts `Ok==true`).
- Fix 3 (blocking — code/api REJECT, LR-005): Added `GetOrder_ReturnsData` happy-path test (mocks `GetOrderAsync` returning `new Order(...)`, asserts `Ok==true` and `Data` non-null).
- Fix 4 (blocking — code/api REJECT, LR-005): Added `GetOrderHistory_ReturnsData` happy-path test (mocks `GetOrderHistoryAsync`, asserts `Ok==true`).
- Fix 5 (blocking — api CONCERN + bad test): Updated `GetBalance_BadAsset` test to assert `Error!.Category == "BadRequest"`.
- Addressed NOTE (trivial): Removed dead `factory.GetClient(id).Returns(client)` setup from `FactoryReturning`.
- Addressed NOTE (trivial): Aligned `client!` null-forgiving in `Run<T>` and `Resolve<T>` to match `MarketDataTools` pattern.
- Deferred: NO-LOWER-BOUND-ON-LIMIT-PARAM — left for follow-up per instructions.
- Build: 0W/0E. Tests: 41 MCP unit tests pass; full suite green.
- Commit: 2de728c

## Review Results

### Attempt 1
- architect-reviewer: CHANGES_REQUESTED (74/100) — missing happy-path tests for GetBalance/GetOrder/GetOrderHistory (LR-005 REJECT)
- code-reviewer: CHANGES_REQUESTED (68/100) — GetOrder/GetOrderHistory zero coverage (LR-005 REJECT) + GetBalance asset guard (LR-001 REJECT)
- security-reviewer: APPROVED (96/100) — no blocking findings
- api-reviewer: CHANGES_REQUESTED (74/100) — wrong error category for bad-asset (REJECT) + missing GetOrder/GetOrderHistory tests (REJECT)

### Attempt 2 (cycle 2 — re-review)
- architect-reviewer: APPROVED (97/100) — LR-005 RESOLVED (all 3 happy-path tests added); no new findings
- code-reviewer: APPROVED (97/100) — LR-005 RESOLVED; LR-001 RESOLVED (comment + ToolResult.Failure direct return); no new findings
- security-reviewer: APPROVED (98/100) — all security properties confirmed; BadRequest direct-return does not bypass security-relevant ToolRunner logic
- api-reviewer: APPROVED (95/100) — WRONG-CATEGORY-FOR-BAD-ASSET RESOLVED; test coverage RESOLVED; BAD-ASSET-CATEGORY-ASSERTION RESOLVED
- **Board verdict: ALL APPROVED — PASS**
- Build: 0W/0E. Tests: 41 MCP unit tests, full suite green. Pre-checks: PASS.
