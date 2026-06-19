# Architect Review — TASK-031

## Verdict: CHANGES_REQUESTED

## Score: 74/100

---

## Findings

### Missing happy-path tests for GetBalance, GetOrder, and GetOrderHistory — REJECT (confidence: 97%)

LR-005: every new service method must have at least one unit test covering the happy path before the task can pass the review gate.

The test file `AccountToolsTests.cs` covers:
- `GetBalances` — happy path + auth error + unknown exchange. PASS.
- `GetOpenOrders` — happy path + auth error. PASS.
- `GetTradeHistory` — happy path. PASS.

Missing happy-path coverage:
- `GetBalance` (line 26–42 of `AccountTools.cs`) — the only test (`GetBalance_BadAsset_ReturnsError`, line 52–58 of `AccountToolsTests.cs`) passes an empty string and exercises only the error branch. There is no test that verifies a known-asset call succeeds and returns a correctly populated `ToolResult`.
- `GetOrder` (line 59–71 of `AccountTools.cs`) — zero tests of any kind.
- `GetOrderHistory` (line 73–84 of `AccountTools.cs`) — zero tests of any kind.

**Fix**: Add a happy-path `[Fact]` for each missing method. For `GetBalance`: substitute `client.Account.GetBalanceAsync(Arg.Any<Asset>(), ...)` to return a valid `AssetBalance`, call `AccountTools.GetBalance(factory, "binance", "BTC")`, and assert `result.Ok` is true. For `GetOrder` and `GetOrderHistory`: follow the same pattern using `client.Trading.GetOrderAsync(...)` and `client.Trading.GetOrderHistoryAsync(...)` respectively.

**Pattern reference**: `AccountToolsTests.cs:96–109` (GetTradeHistory happy-path test is the model to replicate).

---

### GetBalance: asset parameter skips ThrowIfNullOrWhiteSpace — CONCERN (confidence: 72%, non-blocking)

LR-001 states that every public method accepting a non-optional string parameter must call `ArgumentException.ThrowIfNullOrWhiteSpace(param)` as its first statement. `GetBalance` intentionally deviates: a blank/empty `asset` is silently forwarded to `Asset.TryOf`, which returns `false`, causing `ToolRunner.RunAsync` to return a `FormatException`-mapped `ToolError` instead of an `ArgumentException`.

The inline comment (line 33: "empty/whitespace asset → structured ToolError, not ArgumentException") documents the intent, and `MarketDataTools.GetTicker` sets the same precedent for optional-style nullable parameters. For an MCP tool boundary this is a reasonable design choice — the agent receives a structured `ToolError` rather than an unhandled exception. However, the deviation from LR-001 is not formally acknowledged as an approved exception in the rule itself.

**Fix (if hardening is desired)**: Document this deviation in the `LEARNED_RULES.md` as an approved MCP-boundary exception — "at the MCP boundary, non-programming-error string inputs (asset tickers, symbol names) that require domain validation MAY be converted to `ToolError` rather than `ArgumentException`, provided the deviation is commented at the call site." No code change is strictly required.

---

### client null-forgiving operator inconsistency with reference pattern — NOTE (confidence: 85%)

`MarketDataTools.Resolve` (line 116) uses `client!` (null-forgiving) inside the `RunAsync` lambda. `AccountTools.Resolve` (line 119) and `AccountTools.Run` (line 106) use `client` without the `!`. Both compile cleanly — the flow guarantees `factory.TryGet` returned `true` before `client` is passed in — so this is a style inconsistency, not a correctness issue.

**Fix**: Align with the reference pattern: change `call(client, s, default)` to `call(client!, s, default)` in `Resolve` (line 119) and `call(client, default)` to `call(client!, default)` in `Run` (line 106), so future readers see consistent null-assertion discipline across both tool classes.

**Pattern reference**: `MarketDataTools.cs:116` (`return call(client!, s, default)`)

---

## Summary

`AccountTools.cs` is a correct, thin MCP facade over `IAccountService` and the read-only methods of `ITradingService`. It introduces no new exchange logic, no Core/Http edits, no mutating operations (`PlaceOrder`, `CancelOrder` are absent), and follows the `MarketDataTools` structural pattern faithfully. The build is clean (0 warnings, 0 errors with `TreatWarningsAsErrors=true`), and 38 unit tests pass. The blocking issue is test coverage: three of the six public tool methods (`GetBalance`, `GetOrder`, `GetOrderHistory`) lack a happy-path unit test, violating LR-005. Adding three focused happy-path `[Fact]` tests is sufficient to resolve the REJECT and bring the score above threshold.
