# CryptoExchanges.Net.Mcp

A **read-only** [Model Context Protocol (MCP)](https://modelcontextprotocol.io) stdio server that exposes crypto exchange data to LLM agents via 12 structured tools across **Binance, Bybit, OKX, and Bitget** — all returning the same canonical models regardless of exchange.

> **Read-only — no order placement.** This server exposes market-data and account-read operations only. No write or trading tools exist.

---

## Install

```bash
dotnet tool install -g CryptoExchanges.Net.Mcp
```

Requires .NET 10 SDK.

---

## MCP Client Configuration

Add to your MCP client config (e.g. Claude Desktop `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp",
      "env": {
        "BINANCE_API_KEY": "...",
        "BINANCE_SECRET_KEY": "...",
        "BYBIT_API_KEY": "...",
        "BYBIT_SECRET_KEY": "...",
        "OKX_API_KEY": "...",
        "OKX_SECRET_KEY": "...",
        "OKX_PASSPHRASE": "...",
        "BITGET_API_KEY": "...",
        "BITGET_SECRET_KEY": "...",
        "BITGET_PASSPHRASE": "..."
      }
    }
  }
}
```

---

## Environment Variables

| Variable              | Required for        | Description                          |
|-----------------------|---------------------|--------------------------------------|
| `BINANCE_API_KEY`     | Account tools       | Binance API key (read permission)    |
| `BINANCE_SECRET_KEY`  | Account tools       | Binance secret key                   |
| `BYBIT_API_KEY`       | Account tools       | Bybit API key (read permission)      |
| `BYBIT_SECRET_KEY`    | Account tools       | Bybit secret key                     |
| `OKX_API_KEY`         | Account tools       | OKX API key (read permission)        |
| `OKX_SECRET_KEY`      | Account tools       | OKX secret key                       |
| `OKX_PASSPHRASE`      | Account tools       | OKX API passphrase                   |
| `BITGET_API_KEY`      | Account tools       | Bitget API key (read permission)     |
| `BITGET_SECRET_KEY`   | Account tools       | Bitget secret key                    |
| `BITGET_PASSPHRASE`   | Account tools       | Bitget API passphrase                |

**Market-data tools require no credentials** — they use public endpoints only.

---

## Tools (12 total)

### Market Data (6) — no credentials required

| Tool              | Description                                                        |
|-------------------|--------------------------------------------------------------------|
| `GetPrice`        | Latest price for a trading pair on an exchange                     |
| `GetTicker`       | 24h ticker statistics for one pair, or all pairs if symbol omitted |
| `GetOrderBook`    | Order book (bids/asks) for a pair, to the given depth              |
| `GetKlines`       | Candlestick/kline data for a pair at an interval                   |
| `GetRecentTrades` | Recent public trades for a pair                                    |
| `GetExchangeInfo` | Exchange trading rules and the list of supported symbols           |

### Account (6) — read-scoped API credentials required

| Tool               | Description                                                         |
|--------------------|---------------------------------------------------------------------|
| `GetBalances`      | All non-zero asset balances for the account                         |
| `GetBalance`       | Balance of a single asset (e.g. `BTC`)                             |
| `GetOpenOrders`    | Open orders, optionally filtered by pair                            |
| `GetOrder`         | A specific order by its exchange order id                           |
| `GetOrderHistory`  | Historical orders for a pair                                        |
| `GetTradeHistory`  | The account's own executed trades for a pair                        |

---

## Supported Exchanges

Pass `exchange` as one of: `binance`, `bybit`, `okx`, `bitget` (case-insensitive).

## Symbol Format

All tools accept symbols as `BASE/QUOTE`, e.g. `BTC/USDT`, `ETH/USDT`.

## Error Handling

Every tool returns a structured result envelope. On failure the `error.category` field is one of:

| Category             | Meaning                                             |
|----------------------|-----------------------------------------------------|
| `AuthRequired`       | Missing or invalid API credentials                  |
| `RateLimited`        | Exchange rate limit hit                             |
| `ExchangeUnavailable`| Exchange name not recognised                        |
| `Connectivity`       | Network / exchange reachability issue               |
| `SymbolNotSupported` | Unrecognised symbol format                          |
| `BadInterval`        | Unrecognised kline interval string                  |
| `ExchangeError`      | Exchange returned an API-level error                |
| `Unknown`            | Unexpected error                                    |

---

## License

Apache 2.0 — see [LICENSE](../../LICENSE).
