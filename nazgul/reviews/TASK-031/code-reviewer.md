# Code Review — TASK-031

## Verdict: CHANGES_REQUESTED

## Score: 68/100

## Findings

### GetOrder and GetOrderHistory have zero test coverage — REJECT (confidence: 97%)

LR-005: every new service method must have at least one unit test covering the happy path before the task can pass the review gate.

`AccountTools` exposes 6 MCP tools. Only 4 are exercised by the 7 tests: `GetBalances` (3 tests), `GetBalance` (1), `GetOpenOrders` (2), `GetTradeHistory` (1). `GetOrder` and `GetOrderHistory` have no test at all — not a happy-path test, not an auth test, nothing.

The task brief specifies "GetOrderHistory happy" as one of the required 7 test scenarios. The submitted suite substitutes a second `GetOpenOrders` test (auth path) for it. That substitution does not satisfy LR-005 — each new method needs coverage.

**Files**: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs` (no test for `GetOrder` or `GetOrderHistory`)

**Fix**: Add at minimum two tests:
- `GetOrderHistory_ReturnsData` — mock `c.Trading.GetOrderHistoryAsync` returning an empty array, assert `result.Ok == true`.
- `GetOrder_ReturnsData` — mock `c.Trading.GetOrderAsync` returning an `Order`, assert `result.Ok == true` and data is non-null.

---

### `GetBalance` omits `ArgumentException.ThrowIfNullOrWhiteSpace(asset)` — REJECT (confidence: 85%)

LR-001: every public method accepting a non-optional `string` parameter must guard with `ArgumentException.ThrowIfNullOrWhiteSpace` before any other logic.

`GetBalance(IExchangeClientFactory factory, string exchange, string asset)` — `asset` is non-nullable and non-optional. The method guards `factory` and `exchange` correctly but skips the guard for `asset`, with a comment saying empty/whitespace should produce a structured `ToolError` rather than an `ArgumentException`. That rationale is reasonable at an MCP boundary, but it is a deliberate bypass of a hard codebase rule. If the intent is to diverge from LR-001 here, a `#pragma` or `[SuppressMessage]` with a justification comment is required (project convention: `SymbolMapper.cs:46-48`). The current undecorated omission looks like an oversight.

**File**: `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs:30-42`

**Fix (option A — follow LR-001)**: Add `ArgumentException.ThrowIfNullOrWhiteSpace(asset);` after line 32. The `Asset.TryOf` check then only fires for structurally invalid tickers, not empty strings.

**Fix (option B — explicit suppression)**: Keep the current design but add a `#pragma warning disable` with a justification comment (or a `[SuppressMessage(..., Justification = "...")]`), matching the suppression pattern in `SymbolMapper.cs:46-48`. The comment must explain why an MCP tool routes the empty-string case to a structured error instead of throwing.

---

### Bad-asset test does not assert `Error.Category` — CONCERN (confidence: 75%)

`GetBalance_BadAsset_ReturnsError` passes `""` as the asset and asserts only `result.Ok.Should().BeFalse()`. The category produced is `"SymbolNotSupported"` (because `ToolRunner.Categorize` maps `FormatException` to that string). This is semantically wrong for an asset ticker — a caller cannot distinguish "bad symbol format" from "invalid asset ticker" — and the test would pass even if the wrong error path fired. Asserting `result.Error!.Category` would catch regressions. This is non-blocking because `ToolRunner.Categorize` is outside the scope of this task, but the test weakness is worth flagging.

**File**: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs:52-58`

**Suggestion**: Add `result.Error!.Category.Should().Be("SymbolNotSupported");` (or whatever the agreed category is for this path). If the category is wrong at the `ToolRunner` level, surface that as a separate fix.

---

### Dead mock setup in `FactoryReturning` — NOTE (confidence: 90%)

`FactoryReturning` configures `factory.GetClient(id).Returns(client)` (line 17 of the test file), but `AccountTools` never calls `GetClient` — only `TryGet`. The dead setup is harmless but adds noise to every test that uses the helper.

**File**: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/AccountToolsTests.cs:17`

**Suggestion**: Remove the `factory.GetClient(id).Returns(client)` line from `FactoryReturning`.

---

## Summary

The implementation is structurally sound: the `Run<T>` / `Resolve<T>` helpers are clean DRY extractions of the `MarketDataTools` pattern, all 6 tool descriptions are non-empty and informative, the build is clean at 0 warnings / 0 errors, and `Asset.TryOf` bad-input correctly flows through `ToolRunner` to a structured error envelope rather than an unhandled exception.

Two blocking issues prevent approval. First, `GetOrder` and `GetOrderHistory` have no unit tests at all, violating LR-005. Second, the `asset` parameter in `GetBalance` skips `ArgumentException.ThrowIfNullOrWhiteSpace`, violating LR-001 without an accompanying suppression comment. Both are straightforward to fix and do not require structural changes.
