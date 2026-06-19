# API Review — TASK-031

## Verdict: CHANGES_REQUESTED

## Score: 74/100

## Findings

### WRONG-CATEGORY-FOR-BAD-ASSET — REJECT (confidence: 92%)

`GetBalance` throws `FormatException` when `Asset.TryOf(asset, ...)` returns false (line 39 of `AccountTools.cs`). `ToolRunner.Categorize` maps `FormatException` to `"SymbolNotSupported"`. An agent that calls `GetBalance(exchange, "BLAHBLAH")` with a bad asset ticker will receive error category `"SymbolNotSupported"` — semantically wrong. The error is about a bad asset name, not a missing symbol. An agent cannot distinguish "you gave me a bad symbol on GetOrderBook" from "you gave me a bad asset name on GetBalance" because both surface `"SymbolNotSupported"`.

File: `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs:38-39`

Fix: Replace the `FormatException` throw with a dedicated exception that `ToolRunner.Categorize` can map to a meaningful category. The cleanest approach is to throw `ArgumentException` (which currently maps to `"Unknown"`), or better yet, add an `InvalidInputException` arm to `ToolRunner.Categorize`. At minimum, change the throw to something that produces a category the agent can distinguish from symbol parse failures. A pragmatic fix without touching `ToolRunner`:

```csharp
if (!Asset.TryOf(asset, out var a))
    return Task.FromResult(ToolResult<AssetBalance>.Failure(
        new ToolError("BadRequest", $"Unknown asset '{asset}'.")));
```

Return a `ToolResult.Failure` directly (like the `ExchangeUnavailable` early-return path) rather than throwing through `ToolRunner`.

Pattern reference: `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs:71-73` (inline `Failure` return for `BadInterval` before calling `ToolRunner.RunAsync`).

---

### MISSING-TEST-COVERAGE-GET-ORDER-AND-GET-ORDER-HISTORY — REJECT (confidence: 88%)

`GetOrder` and `GetOrderHistory` have zero test coverage in `AccountToolsTests.cs`. The test file covers `GetBalances` (3 tests), `GetBalance` (1 test), `GetOpenOrders` (2 tests), and `GetTradeHistory` (1 test), but `GetOrder` and `GetOrderHistory` are completely untested. Per the Nazgul Rule 4 ("Tests are mandatory. Every task includes tests."), shipping two public tools with no test cases is a blocking issue.

File: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs` (absent)

Fix: Add at minimum:
- `GetOrder_ReturnsData` — happy path with a mocked `GetOrderAsync` returning a valid `Order`.
- `GetOrder_MissingCredentials_MapsToAuthRequired` — throws `AuthenticationException`, asserts `"AuthRequired"`.
- `GetOrderHistory_ReturnsData` — happy path confirming `limit` is forwarded.

---

### NO-LOWER-BOUND-ON-LIMIT-PARAM — CONCERN (confidence: 75%)

`GetOrderHistory` and `GetTradeHistory` accept `int limit = 500` with no minimum-value guard. Passing `limit <= 0` will flow to the exchange API, which may reject it with an opaque error surfaced to the agent as `"ExchangeError"`. A minimum check (e.g. `limit < 1`) returning `ToolError("BadRequest", "limit must be >= 1")` would give the agent an actionable signal.

File: `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs:83-84, 97`

This is consistent with how `MarketDataTools.GetKlines` validates `interval` before forwarding to `ToolRunner`. Non-blocking because the failure mode is recoverable (the agent will get an error back), but it degrades agent experience.

Pattern reference: `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs:71-73`

---

### BAD-ASSET-TEST-LACKS-CATEGORY-ASSERTION — CONCERN (confidence: 85%)

`GetBalance_BadAsset_ReturnsError` (line 53-58 of `AccountToolsTests.cs`) only asserts `result.Ok.Should().BeFalse()`. It does not assert the error category. If the category changes (or is wrong, as noted in WRONG-CATEGORY-FOR-BAD-ASSET above), this test will not catch it. The `AuthRequired` and `ExchangeUnavailable` tests in the same file both assert `.Error!.Category` — this test should do the same.

File: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs:53-58`

Fix: Add `result.Error!.Category.Should().Be("BadRequest")` (after implementing the fix for WRONG-CATEGORY-FOR-BAD-ASSET).

---

### DUPLICATED-EXCHANGE-AND-SYMBOL-PARAM-CONSTANTS — NOTE (confidence: 90%)

`ExchangeParam` and `SymbolParam` are duplicated verbatim from `MarketDataTools.cs` into `AccountTools.cs`. Duplication means the two classes can drift — e.g. if Kraken is added as a supported exchange, someone must update both. The constants are `private const` so extraction to a shared `ToolConstants` internal class is straightforward.

File: `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs:18-19` vs `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs:12-13`

Non-blocking for this task, but worth a follow-up.

---

### API CHECKS — PASS

- **Tool naming (GetBalances, GetBalance, GetOpenOrders, GetOrder, GetOrderHistory, GetTradeHistory)**: All 6 names are clear, agent-readable, and consistent with the MarketDataTools convention. PASS.
- **Parameter names** (`exchange`, `asset`, `symbol`, `orderId`, `limit`): Sensible for LLM consumption. `orderId` correctly describes the exchange-assigned ID; `asset` vs `symbol` distinction is clear. PASS.
- **Description quality**: All `[Description]` annotations accurately describe the tool behavior. The "Requires API credentials" suffix on each is correct and important for agent decision-making. PASS.
- **Default values** (`limit = 500` for GetOrderHistory/GetTradeHistory, `symbol = null` for GetOpenOrders): Match the underlying `IAccountService`/`ITradingService` defaults exactly. Consistent with `MarketDataTools.GetKlines` limit default. PASS.
- **Error categories**: `AuthRequired`, `ExchangeUnavailable`, `RateLimited`, `Connectivity` are all surfaced correctly via `ToolRunner.Categorize` for the happy/unhappy paths. PASS.
- **No interface changes**: This diff adds no members to any Core interface. PASS.
- **No breaking changes to existing API surface**: Both new files are additions only. PASS.
- **Structural pattern consistency**: `AccountTools` follows `MarketDataTools` pattern exactly — `[McpServerToolType]` on the class, `[McpServerTool, Description(...)]` on each method, shared `Run`/`Resolve` helpers, `ArgumentNullException.ThrowIfNull(factory)` guard first. PASS.

---

## Summary

`AccountTools` is well-structured and faithfully follows the `MarketDataTools` pattern for tool naming, description quality, parameter design, and error envelope conventions. Two blocking issues exist: (1) the `GetBalance` bad-asset path incorrectly surfaces error category `"SymbolNotSupported"` when the correct category should be `"BadRequest"` or similar — an agent will misinterpret this as a symbol parse failure rather than a bad asset name, and (2) `GetOrder` and `GetOrderHistory` ship with zero test coverage, violating the mandatory test rule. These must be resolved before merge.
