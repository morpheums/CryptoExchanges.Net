# Architect Review — TASK-030
**Reviewer**: architect-reviewer
**Date**: 2026-06-19
**Verdict**: APPROVED

## Summary

TASK-030 adds `MarketDataTools.cs` (6 read-only MCP tools) and `MarketDataToolsTests.cs` (4 unit tests). The diff introduces no new source files outside of the designated `CryptoExchanges.Net.Mcp` project, makes no changes to any shared layer (Core, Http, or exchange packages), and delegates all exchange interaction through `IExchangeClientFactory`/`IMarketDataService` without embedding any exchange logic. All 29 MCP unit tests pass; the full solution builds clean with `TreatWarningsAsErrors=true` and zero warnings.

## Findings

### PASS Layer containment — no cross-layer pollution (confidence: 99%)

`MarketDataTools.cs` is placed in `src/CryptoExchanges.Net.Mcp/Tools/`, whose `using` directives reference only `CryptoExchanges.Net.Core.Interfaces`, `CryptoExchanges.Net.Core.Models`, and `ModelContextProtocol.Server`. No using or project reference bleeds into Core, Http, or any exchange assembly. The Mcp `.csproj` (already established in TASK-029) holds the exchange references; the tool class itself sees only interfaces.

### PASS Thin-facade constraint honoured (confidence: 99%)

Every tool method resolves the exchange via `ToolInputs.TryParseExchange` + `factory.TryGet`, then delegates to the appropriate `IMarketDataService` method inside `ToolRunner.RunAsync`. Zero exchange-specific logic, zero direct HTTP calls, zero DTO knowledge. The shared private `Resolve<T>` helper correctly eliminates the repetitive boilerplate for the five symbol-required tools.

### PASS Read-only structural constraint (confidence: 100%)

Exactly 6 `[McpServerTool]` methods are present, all mapping to read-only `IMarketDataService` operations (`GetPriceAsync`, `GetTickersAsync`, `GetOrderBookAsync`, `GetCandlesticksAsync`, `GetRecentTradesAsync`, `GetExchangeInfoAsync`). No POST/DELETE path, no `ITradingService` or `IAccountService` reference.

### PASS No new exchange interface members (confidence: 100%)

`IMarketDataService`, `IExchangeClient`, `IExchangeClientFactory` are unchanged. The diff touches no interface file whatsoever.

### PASS No exceptions cross the MCP boundary (confidence: 99%)

`ToolRunner.RunAsync` catches all non-cancellation exceptions and converts them to `ToolResult<T>.Failure`. The three early-return paths (`ExchangeUnavailable`, `BadInterval`) return `Task.FromResult(ToolResult<T>.Failure(...))` directly — no throw possible. `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace` guards are pre-resolution and only fire on programming errors (null `IExchangeClientFactory`), which is correct since the MCP SDK itself provides the factory.

### PASS Test coverage adequacy (confidence: 90%)

The four tests cover: (1) happy-path routing to the correct exchange + decimal return, (2) unknown exchange → `ExchangeUnavailable`, (3) bad symbol format → `SymbolNotSupported` (via `ToolRunner` catching `FormatException` from `ToolInputs.ParseSymbol`), (4) bad interval → `BadInterval`. All four acceptance-criteria paths are exercised. The tests use `NSubstitute` correctly; the `TryGet` out-param stub (`ci[1] = client`) is the established pattern for this interface.

### PASS CancellationToken threading (confidence: 95%)

`default` is passed for every `CancellationToken` argument inside the `ToolRunner.RunAsync` lambda. In the current MCP SDK version used here (1.4.0), the server framework does not surface a per-request `CancellationToken` to static tool methods; passing `default` is the documented approach for this SDK release. `OperationCanceledException` is re-thrown by `ToolRunner` (not swallowed), so if the SDK evolves to propagate a token, cancellation will still propagate correctly.

### CONCERN Static `MarketDataTools` class and Invariant 11 (confidence: 60%, non-blocking)

The plan mandates `static class MarketDataTools` with method-injected `IExchangeClientFactory`. This pattern is a constraint of the MCP SDK's `WithToolsFromAssembly()` discovery mechanism, which reflects static methods and performs parameter injection at call time. The static class here is NOT swappable behavior but an SDK-imposed structural requirement — the MCP framework's discovery and injection model does not support instance-based tool types. This usage is exempt from Invariant 11 (which targets swappable behavior, not framework-mandated structure). However, it is worth noting: if the MCP SDK ever supports instance-based injection (like ASP.NET Core controllers), converting to an injectable class would improve testability marginally. Not a defect at this time given the SDK constraint; no action required.

### CONCERN `ToolInputs` and `ToolRunner` are pre-existing static classes (confidence: 65%, non-blocking)

`ToolInputs` (pure lookup table — parse-only, no I/O) and `ToolRunner` (exception boundary — pure structural, no swappable logic) were established in TASK-029. Neither represents swappable behavior: `ToolInputs` is a fixed symbol/interval/exchange vocabulary, and `ToolRunner` is a deterministic exception-to-category mapping. These fall in the "genuinely fixed pure helpers" category of Invariant 11. This task correctly calls them without modification; the pre-existing pattern is acceptable.

### CONCERN Hardcoded exchange list in `ExchangeParam` description string (confidence: 70%, non-blocking)

`private const string ExchangeParam = "Exchange id: one of binance, bybit, okx, bitget."` duplicates the set of supported exchanges from `ToolInputs.Exchanges`. If a new exchange is added, the description string must be updated manually. This is a maintenance risk as the exchange roster grows (M3+). A future improvement would be to build the description from `ToolInputs.Exchanges.Keys` at class-load time (e.g. a `static readonly` string). Not a blocking issue for the current four-exchange scope.

## Verdict rationale

All hard constraints are satisfied: the diff is contained to the MCP project, delegates through Core interfaces, enforces read-only structure, wraps all exceptions in `ToolResult<T>`, and the build is clean with zero warnings. The three non-blocking concerns are pre-existing or SDK-imposed patterns, none rising above 70% confidence. There are no HIGH/MEDIUM findings at or above the 80% blocking threshold.
