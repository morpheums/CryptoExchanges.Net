---
id: TASK-030
status: IMPLEMENTED
depends_on: [TASK-029]
commit: 91ced11
claimed_at: 2026-06-19T05:00:00Z
---
# TASK-030: Market-data tools (no credentials)

## Metadata
- **ID**: TASK-030
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-029
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs, tests/CryptoExchanges.Net.Mcp.Tests.Unit/MarketDataToolsTests.cs]
- **Wave**: 3
- **Traces to**: Approved design §Tool surface (Market data — no credentials); Approved plan **Task 3**
- **Created at**: 2026-06-19T04:00:00Z
- **Claimed at**: 2026-06-19T05:00:00Z
- **Base SHA**: 2c20c0e
- **Implemented at**: 2026-06-19T05:30:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Status

Blast radius: 1 new tool file + 1 test file. Disjoint from TASK-031 (account tools) — the two
run in parallel in Wave 3. No existing source touched.

## Description

Add the 6 read-only market-data tools. Implement faithfully per **plan Task 3** (it has the
exact `MarketDataTools.cs` + the failing tests). Follow the TDD step order.

`[McpServerToolType] static class MarketDataTools` — 6 static tools, each taking
`IExchangeClientFactory factory` as the FIRST parameter (SDK service injection), returning
`Task<ToolResult<T>>`, with rich `[Description]` on the method and every parameter (the
AI-native differentiator). No credentials required:
- `GetPrice`, `GetTicker` (symbol optional → all), `GetOrderBook` (depth default 100),
  `GetKlines` (interval + limit default 500; bad interval → `BadInterval` error before the call),
  `GetRecentTrades` (limit default 500), `GetExchangeInfo`.

Resolution path: `TryParseExchange` + `factory.TryGet` → on miss return `ExchangeUnavailable`;
otherwise wrap the `IMarketDataService` call in `ToolRunner.RunAsync` (symbol parsed inside so a
bad symbol maps to `SymbolNotSupported`). Use the shared `Resolve<T>` helper for the
symbol-required tools, per the plan. **No write tools.**

## Acceptance Criteria
- [ ] `dotnet build CryptoExchanges.Net.sln -c Release` succeeds with **0 warnings / 0 errors** (TreatWarningsAsErrors).
- [ ] `MarketDataToolsTests` pass (routing→price, unknown exchange→ExchangeUnavailable, bad symbol→SymbolNotSupported, bad interval→BadInterval); existing 455 tests stay green.
- [ ] Exactly 6 `[McpServerTool]` methods exist on `MarketDataTools`, each with a non-empty `[Description]`; none is a write/order operation.

## Pattern Reference
- Service surface being called: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClient.cs`
  (`IMarketDataService`: GetPriceAsync / GetTickersAsync / GetOrderBookAsync /
  GetCandlesticksAsync / GetRecentTradesAsync / GetExchangeInfoAsync).
- Factory being injected: `IExchangeClientFactory` (`GetClient`, `TryGet`, `Available`) —
  `src/CryptoExchanges.Net.DependencyInjection/ExchangeClientFactory.cs`.
- NSubstitute test style: `nazgul/context/test-strategy.md`.
- Exact create code: **plan Task 3, Steps 1–5** — implement as written.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/MarketDataToolsTests.cs

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — Approved design §Tool surface → Market data (6 tools, no credentials)
- **TRD Component**: n/a — `MarketDataTools`
- **ADR Reference**: ADR-001 (thin facade over existing `IExchangeClient` reads; no Core changes)

## Implementation Log

### Attempt 1

- Claimed 2026-06-19T05:00:00Z; base SHA 2c20c0e.
- Created `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs` with 6 `[McpServerTool]` static
  methods: GetPrice, GetTicker, GetOrderBook, GetKlines, GetRecentTrades, GetExchangeInfo.
  All take `IExchangeClientFactory factory` as first parameter (SDK injection) and return
  `Task<ToolResult<T>>`.
- Applied LR-001: `ArgumentException.ThrowIfNullOrWhiteSpace` guards on all non-optional
  string parameters (exchange, symbol, interval); `ArgumentNullException.ThrowIfNull` on factory.
- Created `tests/CryptoExchanges.Net.Mcp.Tests.Unit/MarketDataToolsTests.cs` with 4 tests
  (routing→price, unknown-exchange→ExchangeUnavailable, bad-symbol→SymbolNotSupported,
  bad-interval→BadInterval). TDD: verified compile failure before implementation.
- Build: 0W/0E (`dotnet build CryptoExchanges.Net.sln -c Release`).
- Tests: 29/29 MCP tests pass; 455 existing unit tests green.
- Commit SHA: 91ced11.
- Diff captured to `nazgul/reviews/TASK-030/diff.patch` (197 lines).

## Commits

- `91ced11` feat(FEAT-002): TASK-030 — read-only market-data tools

## Review Results

### Attempt 1
