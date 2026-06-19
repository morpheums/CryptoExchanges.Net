# API Review — TASK-030
**Reviewer**: api-reviewer
**Date**: 2026-06-19
**Verdict**: CHANGES_REQUESTED

## Summary
TASK-030 introduces six read-only MCP market-data tools (`GetPrice`, `GetTicker`, `GetOrderBook`, `GetKlines`, `GetRecentTrades`, `GetExchangeInfo`) implemented as a static class with a consistent `ToolResult<T>` envelope and structured `ToolError` categories. The overall structure, naming, error mapping, and DI injection pattern are sound. One blocking issue exists: the `interval` parameter description (and the underlying `ToolInputs` map) silently omits two `KlineInterval` enum values (`8h` / `3d`) that are valid in the type system but inaccessible from the agent surface, creating a false contract. There are also two non-blocking concerns.

---

## Findings

### REJECT — GetKlines interval description omits `8h` and `3d`; `ToolInputs` map is incomplete (confidence: 95%)
**Severity**: MEDIUM

**Files**:
- `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs:68` — `[Description("Interval: 1m,3m,5m,15m,30m,1h,2h,4h,6h,12h,1d,1w,1M.")]`
- `src/CryptoExchanges.Net.Mcp/ToolInputs.cs:18-28` — the `Intervals` dictionary

**Issue**: `KlineInterval` has 15 members including `EightHours` and `ThreeDays` (see `src/CryptoExchanges.Net.Core/Enums/KlineInterval.cs:25,31`). Neither `"8h"` nor `"3d"` appears in `ToolInputs.Intervals`. An LLM agent reading the `[Description]` will see the full supported list and correctly conclude `8h` and `3d` are absent — but a developer extending the enum later will add a value that the description does not advertise, and the map will silently discard it. More critically for an agent: if an exchange supports 8h candles, the agent has no way to request them even though the core type models the interval.

**Fix**: Add `["8h"] = KlineInterval.EightHours` and `["3d"] = KlineInterval.ThreeDays` to `ToolInputs.Intervals`. Update the `[Description]` text to `"Interval: 1m,3m,5m,15m,30m,1h,2h,4h,6h,8h,12h,1d,3d,1w,1M."` to match.

---

### CONCERN — `interval` lookup is case-sensitive; description gives no case hint (confidence: 85%)
**Severity**: LOW

**File**: `src/CryptoExchanges.Net.Mcp/ToolInputs.cs:18` — `new(StringComparer.Ordinal)`

**Issue**: `"1M"` (month) vs `"1m"` (minute) must be distinguished, so `Ordinal` comparison is intentional. However, an LLM might supply `"1H"`, `"1D"`, or `"1W"` (all-caps) and receive a `BadInterval` error with no indication that casing matters. The description does not mention case sensitivity.

**Fix**: Add `"Case-sensitive."` to the interval `[Description]` — e.g. `"Interval (case-sensitive): 1m,3m,..."`. This costs three words and gives the agent actionable information when it receives a `BadInterval` error.

---

### CONCERN — Test name `GetKlines_BadInterval_ReturnsSymbolNotSupported_OrValidationError` is misleading (confidence: 82%)
**Severity**: LOW

**File**: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/MarketDataToolsTests.cs:55`

**Issue**: The test name contains `ReturnsSymbolNotSupported_OrValidationError` but the assertion checks `result.Error!.Category.Should().Be("BadInterval")` — the "or" is misleading and `SymbolNotSupported` is not the actual category being asserted. A future reader may be confused about what this test actually exercises.

**Fix**: Rename to `GetKlines_BadInterval_ReturnsBadIntervalError` to match the single concrete assertion.

---

## Verdict rationale

The tool naming, `ToolResult<T>` envelope, error category taxonomy (`ExchangeUnavailable`, `SymbolNotSupported`, `BadInterval`), parameter names, defaults (`depth=100`, `limit=500`), and read-only contract are all well-designed and agent-legible. The `Resolve` helper correctly centralises exchange resolution and symbol parsing, and `ToolRunner.RunAsync` cleanly maps exceptions to structured errors.

The blocking finding is the `KlineInterval.EightHours` / `ThreeDays` gap: the type system models these values, the exchange implementations may use them, but the agent surface has no path to reach them. This is a false negative that will silently return `BadInterval` on a valid conceptual request. Because this is a schema-completeness defect in the MCP tool surface (the "API" for agents), and it is easy to fix, it must be resolved before approval.

The two concerns (case sensitivity hint, test name) are non-blocking and can be addressed in the same pass.
