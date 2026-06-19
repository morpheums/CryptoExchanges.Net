# Efficiency Review — TASK-030

**Reviewer**: Efficiency Agent
**Files reviewed**:
- `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs`
- `tests/CryptoExchanges.Net.Mcp.Tests.Unit/MarketDataToolsTests.cs`

---

## Findings

### Finding 1: Task.FromResult on guard-clause early-returns
- **Severity**: LOW
- **Confidence**: 90
- **File**: `MarketDataTools.cs:36, 70, 72-73, 102, 112`
- **Category**: Code Quality
- **Verdict**: PASS
- **Issue**: `Task.FromResult<ToolResult<T>>(...)` allocates a Task wrapper on each guard-clause hit. Caching is not feasible because `ToolResult<T>` is generic and the error message embeds the runtime `exchange` string — no two failure results are structurally identical.
- **Fix**: None required. Standard idiom for synchronous early-returns in Task-returning methods; error path only.

---

### Finding 2: Unavailable(string exchange) allocates a new record and interpolated string on every call
- **Severity**: LOW
- **Confidence**: 85
- **File**: `MarketDataTools.cs:120-121`
- **Category**: Code Quality
- **Verdict**: PASS
- **Issue**: The interpolation embeds the caller-supplied `exchange` string, making caching impossible. On the error path of a network-bound MCP tool, this allocation is irrelevant. Including the bad input in the message is the correct trade-off for debuggability.
- **Fix**: None required.

---

### Finding 3: GetKlines constructs BadInterval ToolError inline instead of via a named helper — inconsistency
- **Severity**: LOW
- **Confidence**: 75
- **File**: `MarketDataTools.cs:72-73`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking, confidence 75/100)
- **Issue**: Every other error path uses `Unavailable(exchange)`. `GetKlines` constructs `new ToolError("BadInterval", $"Unsupported interval '{interval}'.")` inline at the call site. Not an efficiency issue, but a minor consistency gap.
- **Fix**: Extract a private `BadInterval(string interval)` helper mirroring `Unavailable`. Not blocking.

---

### Finding 4: GetTicker symbol-parse inside lambda — intentional
- **Severity**: LOW
- **Confidence**: 70
- **File**: `MarketDataTools.cs:37-42`
- **Category**: Correctness / Code Quality
- **Verdict**: PASS
- **Issue**: `GetTicker` cannot use `Resolve<T>` because `symbol` is optional. The `ParseSymbol` call inside the `ToolRunner.RunAsync` lambda is consistent with how `Resolve<T>` itself works (also parses inside the lambda). `FormatException` from a bad symbol is caught and mapped to `"SymbolNotSupported"` by `ToolRunner.Categorize`.
- **Fix**: None required.

---

### Finding 5: FactoryReturning in tests — correct pattern
- **Severity**: LOW
- **Confidence**: 95
- **File**: `MarketDataToolsTests.cs:13-20`
- **Category**: Testing
- **Verdict**: PASS
- **Issue**: Each test creates a fresh `IExchangeClientFactory` substitute. This is the correct NSubstitute pattern — sharing mocks across tests would cause interference. The `ci => { ci[1] = client; return true; }` delegate is the standard NSubstitute approach for `out`-parameter mocking.
- **Fix**: None required.

---

### Finding 6: No missed concurrency
- **Severity**: N/A
- **Confidence**: 95
- **File**: `MarketDataTools.cs` (entire file)
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: Each tool method resolves a single exchange and executes a single async operation. No multi-exchange fan-out, no list of symbols to fetch, no secondary lookup after the primary. Request-level concurrency is handled by the MCP server infrastructure layer.
- **Fix**: None required.

---

## Summary

- PASS: `Task.FromResult` on guard early-returns — unavoidable; error path only
- PASS: `Unavailable(string exchange)` allocation — message embeds input; caching impossible; irrelevant cost on error path
- PASS: `FactoryReturning` in tests — standard NSubstitute pattern; fresh mocks per test is correct
- PASS: `GetTicker` symbol-parse inside lambda — consistent with `Resolve<T>`; intentional design
- PASS: No missed concurrency — single exchange / single operation per tool call
- CONCERN: `GetKlines` constructs `new ToolError("BadInterval", ...)` inline while every other error path goes through a named helper — minor consistency gap, no runtime impact (confidence: 75/100, non-blocking)

---

## Final Verdict

APPROVED
