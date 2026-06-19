# Security Reviewer — TASK-037

## Verdict: CHANGES_REQUESTED

## Findings

### REJECT: `library-usage.md` links to a non-existent file that positions the library as AI-agent-native (confidence: 90%)

`docs/library-usage.md:391` contains:
```
- [MCP server](mcp-server.md) — AI-agent read-only access via the Model Context Protocol
```

Two violations in one line:
1. `mcp-server.md` does not exist in `docs/`. A broken link in a shipped doc page is a documentation correctness failure, but more importantly:
2. The description "AI-agent read-only access via the Model Context Protocol" constitutes AI/agent positioning language. The opsec constraint explicitly prohibits "AI/agent positioning." This phrasing frames the library as an AI-agent-native product, which is internal strategic positioning that must not appear in public-facing docs.

The MCP server feature itself is not part of this TASK-037 doc set, and neither `mcp-server.md` nor the feature it describes exists as a published artifact in scope of this task. The link should be removed entirely.

**File:** `docs/library-usage.md:391`

---

### REJECT: `architecture.md` names `BinanceSigningHandler` as a shared/generic component in the handler chain diagram (confidence: 85%)

`docs/architecture.md:101` reads:
```
BinanceSigningHandler         — adds timestamp + HMAC-SHA256 signature (per-exchange impl)
```

The diagram is introduced as the shared Http layer handler chain ("Shared, exchange-agnostic HTTP infrastructure"). Naming `BinanceSigningHandler` here is misleading and technically wrong: the signing handler is per-exchange (`BinanceSigningHandler`, `BybitSigningHandler`, `OkxSigningHandler`, `BitgetSigningHandler`). Shipping this description as a public architecture reference creates an incorrect security model in the reader's mind — they could conclude that only one signing handler exists, missing OKX/Bitget's passphrase-header signing entirely, or that the handler is in the shared Http layer rather than per-exchange packages.

This is a documentation-accuracy issue with security implications (incorrect signing mental model). The label should be `*SigningHandler (per-exchange)` or `[Exchange]SigningHandler` to match the table in the same file at line 128 ("*SigningHandler — DelegatingHandler that attaches the signature to each outbound request").

**File:** `docs/architecture.md:101`

---

### CONCERN: `library-usage.md` mentions `RawBody` availability without a "do not log in production" caution (confidence: 70%)

`docs/library-usage.md:366-367` states:
`ExchangeApiException` exposes an optional integer `Code` (the exchange's raw numeric error code) and the raw response body for diagnostics.

The system context for this project notes: "Raw body is available on `ExchangeApiException.RawBody` — callers should not log this in production." The docs surface `RawBody` as a diagnostics aid but provide no guidance warning against logging it in production. Exchange error response bodies can contain partial request context. A one-sentence caution ("Do not log `RawBody` in production; it may contain sensitive request data.") would close this gap.

**File:** `docs/library-usage.md:366-367`

---

### PASS: No hardcoded real secrets (confidence: 100%)
All credential fields in code examples use safe placeholder values: `"..."` in `exchanges.md` and `"your-api-key"` / `"your-secret-key"` / `"your-passphrase"` in `getting-started.md`. No real-looking keys (length, character patterns, base64) appear anywhere.

### PASS: Env-var guidance is correct and complete (confidence: 100%)
All ten env var names (`BINANCE_API_KEY`, `BINANCE_SECRET_KEY`, `BYBIT_API_KEY`, `BYBIT_SECRET_KEY`, `OKX_API_KEY`, `OKX_SECRET_KEY`, `OKX_PASSPHRASE`, `BITGET_API_KEY`, `BITGET_SECRET_KEY`, `BITGET_PASSPHRASE`) verified against source (`BinanceExchangeClient.cs:75-76`, `BybitExchangeClient.cs:77-78`, `OkxExchangeClient.cs:77-79`, `BitgetExchangeClient.cs:77-79`). Names match exactly.

### PASS: OKX and Bitget passphrase guidance is correct (confidence: 100%)
Both exchanges document the passphrase as a mandatory third credential, explain it is set at key-creation time, and show it in all three code paths (direct options, `CreateFromEnvironment`, DI registration). The callout blocks (`> **Passphrase required.**`) are prominent and accurate.

### PASS: No WebSocket mentions in the four doc pages (confidence: 100%)
Searched all four files. No WebSocket references found.

### PASS: No gateway/proxy/AI-positioning/monetization/competitive language in the four doc pages (confidence: 100%)
The sole violation is the `mcp-server.md` link in `library-usage.md` (covered in REJECT above). No gateway, proxy, pricing, or competitive analysis language appears in any of the four doc pages.

### PASS: "Coming soon" scope is exchanges only (confidence: 100%)
`exchanges.md:229-237` lists Coinbase, Kraken, and KuCoin only. No feature roadmap items appear.

### PASS: `Environment.GetEnvironmentVariable(...)` usage is safe and correct (confidence: 100%)
The explicit-options path in `getting-started.md:104-108` demonstrates `Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? ""` — correct, no raw secrets in code.

### PASS: DI configuration examples use `IConfiguration` (confidence: 100%)
All DI examples use `configuration["Binance:ApiKey"]` / `builder.Configuration[...]` — correct indirection via .NET configuration, not inline secrets.

### PASS: No exchange testnet/sandbox URLs mentioned (confidence: 100%)
No exchange URLs appear in any of the four files; no testnet vs. mainnet confusion risk.

### PASS: Architecture doc scoped to shipped design (confidence: 100%)
`architecture.md:2` explicitly states "This page describes the shipped design only." No future features or internal strategy mentioned.

## Summary

Two blocking findings:
1. `library-usage.md:391` links to a non-existent `mcp-server.md` and uses prohibited AI/agent positioning language. Remove the line.
2. `architecture.md:101` names `BinanceSigningHandler` as if it is the shared handler in the Http layer, which misrepresents the per-exchange signing architecture. Change to `*SigningHandler (per-exchange)`.

One non-blocking concern: the mention of `RawBody` for diagnostics should carry a "do not log in production" advisory.

