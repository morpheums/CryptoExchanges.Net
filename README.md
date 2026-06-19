# CryptoExchanges.Net

> **A unified .NET SDK for cryptocurrency exchanges — one typed interface across every exchange, with a read-only MCP server for AI agents.**

[![NuGet](https://img.shields.io/badge/nuget-v0.2.0--preview.1-blue)](https://www.nuget.org/)
[![License](https://img.shields.io/badge/license-Apache--2.0-green)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

---

## Supported Exchanges

| Exchange | Status | Package |
|----------|--------|---------|
| <img src="docs/assets/exchanges/binance.svg" width="20"> Binance | ✅ Supported | `CryptoExchanges.Net.Binance` |
| <picture><source media="(prefers-color-scheme: dark)" srcset="docs/assets/exchanges/bybit-dark.svg"><img src="docs/assets/exchanges/bybit-light.svg" width="20"></picture> Bybit | ✅ Supported | `CryptoExchanges.Net.Bybit` |
| <picture><source media="(prefers-color-scheme: dark)" srcset="docs/assets/exchanges/okx-dark.svg"><img src="docs/assets/exchanges/okx-light.svg" width="20"></picture> OKX | ✅ Supported | `CryptoExchanges.Net.Okx` |
| <img src="docs/assets/exchanges/bitget.svg" width="20"> Bitget | ✅ Supported | `CryptoExchanges.Net.Bitget` |
| <img src="docs/assets/exchanges/coinbase.svg" width="20"> Coinbase | 🕓 Coming soon | — |
| <img src="docs/assets/exchanges/kraken.svg" width="20"> Kraken | 🕓 Coming soon | — |
| <img src="docs/assets/exchanges/kucoin.svg" width="20"> KuCoin | 🕓 Coming soon | — |

REST, spot market data and account — read and write.

---

## 60-Second Quick Start

### Library

```bash
dotnet add package CryptoExchanges.Net.Binance
```

```csharp
await using var exchange = BinanceExchangeClient.Create(new BinanceOptions
{
    ApiKey    = "your-api-key",
    SecretKey = "your-secret-key",
});

var price = await exchange.MarketData.GetPriceAsync(new Symbol(Asset.Btc, Asset.Usdt));
Console.WriteLine($"BTC/USDT: ${price}");
```

### MCP Server (AI agents)

```bash
dotnet tool install -g CryptoExchanges.Net.Mcp
claude mcp add crypto -- crypto-mcp
```

Your MCP-capable agent can now query live prices, order books, candles, and account balances across all four exchanges.

---

## MCP Server

`CryptoExchanges.Net.Mcp` is a **read-only** [Model Context Protocol](https://modelcontextprotocol.io) stdio server.
It exposes **12 tools** — six market-data tools (no credentials required) and six account tools (read-scoped API keys).
All four supported exchanges share the same tool vocabulary; no agent-side changes needed when switching exchanges.

- [MCP server reference](docs/mcp-server.md) — tools, environment variables, error handling
- [MCP client setup guides](docs/mcp-clients.md) — Claude Desktop, Claude Code, Cursor, Windsurf, and more

---

## Documentation

| Doc | Description |
|-----|-------------|
| [Getting started](docs/getting-started.md) | Install, credentials, first call |
| [Library usage](docs/library-usage.md) | Full API reference with examples |
| [Architecture](docs/architecture.md) | Project structure, layers, design principles |
| [Exchanges](docs/exchanges.md) | Per-exchange notes, credentials, supported operations |
| [MCP server](docs/mcp-server.md) | MCP tool reference, env vars, error categories |
| [MCP client setup](docs/mcp-clients.md) | Per-client config (Claude, Cursor, Windsurf, VS Code…) |

---

## Building

```bash
dotnet build
dotnet test
```

Requires .NET 10.0 SDK.

---

## License

Apache-2.0 — see [LICENSE](LICENSE).

---

Built by [Morpheums](https://github.com/morpheums).
