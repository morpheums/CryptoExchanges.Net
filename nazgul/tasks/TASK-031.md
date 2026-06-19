---
id: TASK-031
status: PLANNED
depends_on: [TASK-029]
commit:
claimed_at:
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
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

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
- [ ] `dotnet build CryptoExchanges.Net.sln -c Release` succeeds with **0 warnings / 0 errors** (TreatWarningsAsErrors).
- [ ] `AccountToolsTests` pass (balances→data, missing-credentials→AuthRequired, bad-asset→error); existing 455 tests stay green.
- [ ] Exactly 6 `[McpServerTool]` methods exist on `AccountTools`, all read-only (no Place/Cancel/Create/Submit/Delete); each has a non-empty `[Description]`.

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

## Review Results

### Attempt 1
