# MCP Server

CryptoExchanges.Net ships a **read-only**
[Model Context Protocol (MCP)](https://modelcontextprotocol.io) stdio server that exposes
crypto exchange data to LLM agents via 12 structured tools across **Binance, Bybit, OKX, and
Bitget** — all returning the same canonical models regardless of exchange.

> **Read-only — no order placement.** This server exposes market-data and account-read
> operations only. No write or trading tools exist.

---

## Install

Requires .NET 10 SDK.

```bash
dotnet tool install -g CryptoExchanges.Net.Mcp
```

The global tool command is `crypto-mcp`. After install, verify with:

```bash
crypto-mcp --version
```

---

## How it works

`crypto-mcp` is a **local stdio MCP server** — it runs as a subprocess managed by your MCP
client (Claude Desktop, Cursor, etc.). There is no network port and no persistent daemon; the
client spawns it on demand.

Each tool call translates directly to a read-only REST request to the chosen exchange and
returns a structured result envelope. On failure the envelope carries a typed `error.category`
(see [Error categories](#error-categories) below) rather than throwing.

---

## Tools (12 total)

The canonical tool reference lives in
[`src/CryptoExchanges.Net.Mcp/README.md`](../src/CryptoExchanges.Net.Mcp/README.md).
The tables below mirror that source and are kept in sync with it.

### Market data (6) — no credentials required

| Tool              | Description                                                         |
|-------------------|---------------------------------------------------------------------|
| `GetPrice`        | Latest price for a trading pair on an exchange                      |
| `GetTicker`       | 24 h ticker statistics for one pair, or all pairs if symbol omitted |
| `GetOrderBook`    | Order book (bids/asks) for a pair, to the given depth               |
| `GetKlines`       | Candlestick/kline data for a pair at an interval                    |
| `GetRecentTrades` | Recent public trades for a pair                                     |
| `GetExchangeInfo` | Exchange trading rules and the list of supported symbols            |

### Account (6) — read-scoped API credentials required

Supply credentials as environment variables (see [Credentials](#credentials) below).

| Tool               | Description                                                          |
|--------------------|----------------------------------------------------------------------|
| `GetBalances`      | All non-zero asset balances for the account                          |
| `GetBalance`       | Balance of a single asset (e.g. `BTC`)                              |
| `GetOpenOrders`    | Open orders, optionally filtered by pair                             |
| `GetOrder`         | A specific order by its exchange order ID                            |
| `GetOrderHistory`  | Historical orders for a pair                                         |
| `GetTradeHistory`  | The account's own executed trades for a pair                         |

---

## Symbol format

All tools accept symbols as `BASE/QUOTE` (e.g., `BTC/USDT`, `ETH/USDT`, `SOL/BTC`).

---

## Supported exchanges

Pass `exchange` as one of: `binance`, `bybit`, `okx`, `bitget` (case-insensitive).

---

## Credentials

Set environment variables before starting your MCP client (or pass them in the client's `env`
block — see [mcp-clients.md](mcp-clients.md)).

**Market-data tools need no credentials** — they use public endpoints only.

| Variable              | Required for  | Description                               |
|-----------------------|---------------|-------------------------------------------|
| `BINANCE_API_KEY`     | Account tools | Binance API key (read permission)         |
| `BINANCE_SECRET_KEY`  | Account tools | Binance secret key                        |
| `BYBIT_API_KEY`       | Account tools | Bybit API key (read permission)           |
| `BYBIT_SECRET_KEY`    | Account tools | Bybit secret key                          |
| `OKX_API_KEY`         | Account tools | OKX API key (read permission)             |
| `OKX_SECRET_KEY`      | Account tools | OKX secret key                            |
| `OKX_PASSPHRASE`      | Account tools | OKX API passphrase (third OKX credential) |
| `BITGET_API_KEY`      | Account tools | Bitget API key (read permission)          |
| `BITGET_SECRET_KEY`   | Account tools | Bitget secret key                         |
| `BITGET_PASSPHRASE`   | Account tools | Bitget API passphrase                     |

**OKX and Bitget require a passphrase** — a third credential configured in the exchange's API
management console alongside the key and secret.

Unused exchange credentials are silently ignored.

---

## Error categories

Every tool returns a structured result envelope. On failure the `error.category` field is one
of:

| Category              | Meaning                                                    |
|-----------------------|------------------------------------------------------------|
| `AuthRequired`        | Missing or invalid API credentials                         |
| `RateLimited`         | Exchange rate limit hit                                    |
| `Connectivity`        | Network / exchange reachability issue                      |
| `SymbolNotSupported`  | Unrecognised symbol format                                 |
| `ExchangeUnavailable` | Exchange name not recognised                               |
| `BadRequest`          | Invalid argument (e.g. non-positive depth/limit, unknown asset) |
| `BadInterval`         | Unrecognised kline interval string                         |
| `ExchangeError`       | Exchange returned an API-level error                       |
| `Unknown`             | Unexpected error                                           |

---

## Testing with MCP Inspector

You can test the server directly (without a full MCP client) using the official
[MCP Inspector](https://github.com/modelcontextprotocol/inspector):

```bash
npx @modelcontextprotocol/inspector dotnet run --project src/CryptoExchanges.Net.Mcp -c Release
```

This opens a browser UI where you can invoke each tool interactively and inspect the raw
request/response envelopes.

---

## Client Setup

For per-client config blocks (Claude Code, Claude Desktop, Cursor, VS Code, Windsurf, Cline,
Codex CLI, Gemini CLI), see [mcp-clients.md](mcp-clients.md).

---

## License

Apache 2.0 — see [LICENSE](../LICENSE).
