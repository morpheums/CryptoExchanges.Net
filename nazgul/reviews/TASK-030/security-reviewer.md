# Security Review — TASK-030
**Reviewer**: security-reviewer
**Date**: 2026-06-19
**Verdict**: APPROVED

## Summary

TASK-030 introduces read-only public market-data MCP tools (`MarketDataTools.cs`) that require no API credentials. The credential isolation is solid: all tools route through `TryParseExchange` (whitelist lookup), `ToolInputs.ParseSymbol` (typed parse with format enforcement), and `ToolRunner.RunAsync` (exception boundary). No secrets are touched, logged, or transmitted. Two low-severity concerns exist around integer parameter bounds and the reflection of the caller-supplied exchange string in an error message, but neither is blocking.

## Findings

### PASS No credential access in market-data tools (confidence: 100%)

`MarketDataTools.cs` accesses only `IExchangeClientFactory` and `IMarketDataService`. It does not reference `ApiKey`, `SecretKey`, passphrase, or any options class. The signing pipeline (`BinanceSigningRequest.MarkSigned`) is only invoked for signed requests; all calls here use `false` for the signed flag. Credentials never enter this call path.

### PASS Exchange routing uses a closed whitelist (confidence: 100%)

`ToolInputs.TryParseExchange` (ToolInputs.cs:9-16) is a case-insensitive dictionary lookup over exactly four keys: `binance`, `bybit`, `okx`, `bitget`. Arbitrary exchange strings are rejected and produce the typed `ExchangeUnavailable` error. There is no dynamic dispatch, no reflection, and no format string construction from the unvalidated exchange value beyond the error message (see CONCERN below).

### PASS Symbol parsing is safe against injection and ReDoS (confidence: 100%)

`ToolInputs.ParseSymbol` (ToolInputs.cs:40-47) splits on `/`, then validates each part through `Asset.TryOf()`, which the security-surface doc confirms enforces A-Z0-9 up to 32 chars. No regex is involved, so ReDoS is impossible. A malformed symbol throws `FormatException`, which `ToolRunner` catches and maps to `"SymbolNotSupported"` — no raw user input echoed beyond the format-exception message, which itself comes from the SDK format string not the raw input.

### PASS Interval parsing is an exact-match whitelist (confidence: 100%)

`ToolInputs.TryParseInterval` (ToolInputs.cs:35-36) is a pure dictionary lookup over 13 fixed strings. Only the matched string literal `"Unsupported interval '{interval}'"` is reflected in the error (MarketDataTools.cs:73) — this is the exact, already-validated caller input from an MCP agent, not a web user. Acceptable.

### PASS No credential leakage via ex.Message in ToolRunner (confidence: 97%)

`ToolRunner.RunAsync` (ToolRunner.cs:24) surfaces `ex.Message` in the error envelope. For `ExchangeApiException`, `Message` is set to the formatted string `text` (e.g., `"Binance error -1003: Too many requests"`) inside `BinanceErrorTranslator.cs:17`. `RawBody` is a separate property on the exception and is never referenced in `ToolRunner`. The raw response body does not leak into the MCP response.

### PASS stdout/stderr separation (confidence: 100%)

`Program.cs:10` correctly routes all logging to stderr: `AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)`. stdout is exclusively the MCP transport channel. `MarketDataTools` itself performs no logging calls.

### PASS Read-only structural enforcement (confidence: 100%)

All six tools call `IMarketDataService` methods only. No `ITradingService`, `IAccountService`, or order-placement path is reachable from this file. No `MarkSigned()` call exists, so no authenticated requests can be issued.

### CONCERN Exchange string reflected in error message (confidence: 65%)

**Severity**: LOW

`MarketDataTools.cs:120-121` and `MarketDataTools.cs:107-108` construct an error message that echoes the raw `exchange` parameter: `$"Exchange '{exchange}' is not one of: binance, bybit, okx, bitget."`. Because this is a MCP tool (the caller is an LLM agent, not an end user), and the value has already been rejected by `TryParseExchange` before reaching a network call, there is no injection risk. The reflected string goes into a `ToolResult<T>` returned to the agent, not to a log or exception stack. Low severity, non-blocking.

**Suggested improvement**: Cap the echoed string at a safe length to prevent oversized error payloads if a very long exchange string is supplied: `exchange.Length > 64 ? exchange[..64] + "…" : exchange`.

### CONCERN Integer parameters `depth` and `limit` have no upper-bound guard at the MCP layer (confidence: 72%)

**Severity**: LOW

`GetOrderBook` passes `depth` directly to `GetOrderBookAsync`; `GetKlines` and `GetRecentTrades` pass `limit` directly to their respective service methods. The exchange service implementations have inconsistent clamping behavior:

- Binance (`BinanceMarketDataService.cs:75`, `99`, `155`): no clamping — sends the caller-supplied integer verbatim.
- Bybit (`BybitMarketDataService.cs:72`, `99`, `157`): no clamping.
- OKX (`OkxMarketDataService.cs:64`, `93`, `145`): clamps with `Math.Clamp` and `Math.Min`.
- Bitget (`BitgetMarketDataService.cs:63`, `93`, `145`): clamps with `Math.Clamp`.

For Binance and Bybit, a very large `depth` or `limit` (e.g., `Int32.MaxValue`) is forwarded to the exchange API, which will reject it with a 400 error translated to `ExchangeApiException`. The MCP boundary catches this and returns a structured failure — no crash, no panic. There is no server-side resource exhaustion risk because the actual response size is controlled by the exchange. Risk is limited to triggering a useless API call that the exchange rejects. Non-blocking but worth a future hardening pass.

**Suggested improvement**: Add a guard at the MCP tool layer: `depth = Math.Clamp(depth, 1, 5000)` and `limit = Math.Clamp(limit, 1, 5000)` before passing through, so MCP tools are uniformly safe regardless of which exchange handles the downstream clamping.

## Verdict rationale

Both concerns are below the 80% confidence threshold for blocking rejection. The credential isolation is complete and correct. The whitelist routing, symbol parsing, interval parsing, stdout/stderr separation, and read-only structural enforcement all follow the established codebase patterns. The two low-severity concerns (reflected string length, integer bounds) are defense-in-depth improvements that do not represent exploitable vulnerabilities in the MCP context.
