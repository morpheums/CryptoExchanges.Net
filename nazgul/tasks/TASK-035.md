---
id: TASK-035
status: IN_PROGRESS
commit:
claimed_at: 2026-06-19
---

# TASK-035: Address PR #19 code-review findings (4)

**Status**: READY

**Blast radius**: LOW — one type-file split, one dead-test-line removal, two doc fixes. No behavior change; tests stay green.

## Findings (from PR #19 review)
1. `src/CryptoExchanges.Net.Mcp/ToolResult.cs` declares two top-level types (`ToolError` + `ToolResult<T>`) — violates the CLAUDE.md one-type-per-file rule. Extract `ToolError` into `ToolError.cs`.
2. Dead mock stub in `MarketDataToolsTests.FactoryReturning`: `factory.GetClient(id).Returns(client)` is never exercised (tools route via `TryGet`). Remove it (matches the AccountToolsTests fix from TASK-031).
3. MCP `README.md` error-category table omits `BadRequest` (tools emit it). Add it.
4. `CHANGELOG.md` lists categories that don't exist (`InvalidSymbol`, `InsufficientBalance`). Replace with the real set from `ToolRunner.Categorize` + tool emitters: `AuthRequired`, `RateLimited`, `Connectivity`, `SymbolNotSupported`, `ExchangeUnavailable`, `BadRequest`, `BadInterval`, `ExchangeError`, `Unknown`.

## Acceptance
- Build 0W/0E; all MCP tests green; `ToolResult.cs` and `ToolError.cs` each hold exactly one type.
