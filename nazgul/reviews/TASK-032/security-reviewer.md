# Security Review — TASK-032

**Verdict**: APPROVED
**Confidence**: 95

## Summary

The diff is documentation-only plus a reflection-based test guard and a `.csproj` pack fix — no new runtime credential-handling code is introduced. All credential flow paths (env var reading, signing, transmission) are unchanged from the previously reviewed implementation in `EnvCredentialBinder.cs`. The README correctly uses placeholder values (`"..."`), lists `OKX_PASSPHRASE` and `BITGET_PASSPHRASE`, and advises read-permission keys on every API-key row. The read-only claim is accurate: `AccountTools.cs` and `MarketDataTools.cs` contain only query/read operations with no write-verb methods.

## Findings

### MCP config JSON uses safe placeholders — PASS (98%)
The MCP client configuration block in `src/CryptoExchanges.Net.Mcp/README.md:23-43` uses `"..."` as the value for every credential field. No real keys or secrets are hardcoded. This is the correct pattern for example configuration.

### OKX_PASSPHRASE and BITGET_PASSPHRASE documented and implemented — PASS (99%)
Both passphrases appear in the README env-var table at lines 57 and 60, in the MCP client config JSON block at lines 35 and 38-39, and are correctly consumed in `EnvCredentialBinder.cs:20,23`. The names match exactly between documentation and code.

### Read permission guidance present on API key rows — PASS (97%)
Every `*_API_KEY` row in the env-var table carries "(read permission)" in the description column (README lines 51, 53, 55, 58). The intro callout block (line 5) and root README (line 234) both state "no order placement." The section heading for account tools (line 79) reads "read-scoped API credentials required." The guidance is present and clear.

### Secret key rows lack explicit "read permission" annotation — CONCERN (55%)
The `*_SECRET_KEY` rows in the env-var table (lines 52, 54, 56, 59) do not carry a "(read permission)" note, whereas the corresponding `*_API_KEY` rows do. Secret keys are not separately scopable on any of these exchanges — the scope is set at the API key level — so the omission is technically correct. However, a reader scanning only the secret-key row will not see the permission reminder. This is a documentation clarity issue only, not a security defect, and is non-blocking given the read-only claim is stated three times elsewhere.

### Read-only claim verified against AccountTools.cs — PASS (100%)
All six `[McpServerTool]` methods in `AccountTools.cs` call read-only interface members: `GetBalancesAsync`, `GetBalanceAsync`, `GetOpenOrdersAsync`, `GetOrderAsync`, `GetOrderHistoryAsync`, `GetTradeHistoryAsync`. No placement, cancellation, withdrawal, or state-mutating calls are present.

### Read-only claim verified against MarketDataTools.cs — PASS (100%)
All six `[McpServerTool]` methods in `MarketDataTools.cs` call public market-data reads: `GetPriceAsync`, `GetTickersAsync`, `GetOrderBookAsync`, `GetCandlesticksAsync`, `GetRecentTradesAsync`, `GetExchangeInfoAsync`. No write operations exist.

### No hardcoded secrets anywhere in the diff — PASS (99%)
Full grep of the diff and all touched source files finds no literal API keys, secret keys, passphrases, or any non-placeholder credential value.

### ToolRosterTests write-verb guard covers the right verbs — PASS (95%)
The banned list in `ToolRosterTests.cs:207` includes `Place`, `Cancel`, `Create`, `Submit`, `Delete`. This covers standard trading write operations. "Withdraw" and "Transfer" are not in the list, but no such methods exist in the current tools and the test is a regression guard against future addition of banned-name tools, not a whitelist exhaustion. Non-blocking.

## Rule References
None
