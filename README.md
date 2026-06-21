<p align="center">
  <img src="https://raw.githubusercontent.com/OrodruinLabs/CryptoExchanges.Net/main/docs/assets/banner.png" alt="CryptoExchanges.Net — one typed .NET interface across every exchange, with a read-only MCP server for AI agents" width="100%">
</p>

# CryptoExchanges.Net

> **A unified .NET SDK for cryptocurrency exchanges — one typed interface across every exchange, with a read-only MCP server for AI agents.**

[![NuGet](https://img.shields.io/nuget/v/CryptoExchanges.Net.svg)](https://www.nuget.org/packages/CryptoExchanges.Net)
[![License](https://img.shields.io/badge/license-Apache--2.0-green)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Downloads](https://img.shields.io/nuget/dt/CryptoExchanges.Net.svg)](https://www.nuget.org/packages/CryptoExchanges.Net)

---

## Supported Exchanges

| Exchange | Status | Package |
|----------|--------|---------|
| <img src="docs/assets/exchanges/binance.svg?v=2" width="20"> Binance | ✅ Supported | [![Binance package](https://img.shields.io/nuget/v/CryptoExchanges.Net.Binance?logo=nuget&label=CryptoExchanges.Net.Binance)](https://www.nuget.org/packages/CryptoExchanges.Net.Binance) [![Binance downloads](https://img.shields.io/nuget/dt/CryptoExchanges.Net.Binance?logo=nuget&label=downloads)](https://www.nuget.org/packages/CryptoExchanges.Net.Binance) |
| <picture><source media="(prefers-color-scheme: dark)" srcset="docs/assets/exchanges/bybit-dark.svg?v=2"><img src="docs/assets/exchanges/bybit-light.svg?v=2" width="20"></picture> Bybit | ✅ Supported | [![Bybit package](https://img.shields.io/nuget/v/CryptoExchanges.Net.Bybit?logo=nuget&label=CryptoExchanges.Net.Bybit)](https://www.nuget.org/packages/CryptoExchanges.Net.Bybit) [![Bybit downloads](https://img.shields.io/nuget/dt/CryptoExchanges.Net.Bybit?logo=nuget&label=downloads)](https://www.nuget.org/packages/CryptoExchanges.Net.Bybit) |
| <picture><source media="(prefers-color-scheme: dark)" srcset="docs/assets/exchanges/okx-dark.svg?v=2"><img src="docs/assets/exchanges/okx-light.svg?v=2" width="20"></picture> OKX | ✅ Supported | [![OKX package](https://img.shields.io/nuget/v/CryptoExchanges.Net.Okx?logo=nuget&label=CryptoExchanges.Net.Okx)](https://www.nuget.org/packages/CryptoExchanges.Net.Okx) [![OKX downloads](https://img.shields.io/nuget/dt/CryptoExchanges.Net.Okx?logo=nuget&label=downloads)](https://www.nuget.org/packages/CryptoExchanges.Net.Okx) |
| <img src="docs/assets/exchanges/bitget.svg?v=2" width="20"> Bitget | ✅ Supported | [![Bitget package](https://img.shields.io/nuget/v/CryptoExchanges.Net.Bitget?logo=nuget&label=CryptoExchanges.Net.Bitget)](https://www.nuget.org/packages/CryptoExchanges.Net.Bitget) [![Bitget downloads](https://img.shields.io/nuget/dt/CryptoExchanges.Net.Bitget?logo=nuget&label=downloads)](https://www.nuget.org/packages/CryptoExchanges.Net.Bitget) |
| <img src="docs/assets/exchanges/coinbase.svg?v=2" width="20"> Coinbase | 🕓 Coming soon | — |
| <img src="docs/assets/exchanges/kraken.svg?v=2" width="20"> Kraken | 🕓 Coming soon | — |
| <img src="docs/assets/exchanges/kucoin.svg?v=2" width="20"> KuCoin | ✅ Supported | [![KuCoin package](https://img.shields.io/nuget/v/CryptoExchanges.Net.Kucoin?logo=nuget&label=CryptoExchanges.Net.Kucoin)](https://www.nuget.org/packages/CryptoExchanges.Net.Kucoin) [![KuCoin downloads](https://img.shields.io/nuget/dt/CryptoExchanges.Net.Kucoin?logo=nuget&label=downloads)](https://www.nuget.org/packages/CryptoExchanges.Net.Kucoin) |

REST, spot market data and account — read and write.

<sub>Exchange names and logos are trademarks of their respective owners. CryptoExchanges.Net is an independent open-source project and is not affiliated with, endorsed by, or sponsored by any listed exchange.</sub>

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

Your MCP-capable agent can now query live prices, order books, candles, and account balances across all supported exchanges.

---

## MCP Server

`CryptoExchanges.Net.Mcp` is a **read-only** [Model Context Protocol](https://modelcontextprotocol.io) stdio server.
It exposes **12 tools** — six market-data tools (no credentials required) and six account tools (read-scoped API keys).
All supported exchanges share the same tool vocabulary; no agent-side changes needed when switching exchanges.

- [MCP server reference](docs/mcp-server.md) — tools, environment variables, error handling
- [MCP client setup guides](docs/mcp-clients.md) — Claude Desktop, Claude Code, Cursor, Windsurf, and more

---

## Documentation

| Doc | Description |
|-----|-------------|
| [Getting started](docs/getting-started.md) | Install, credentials, first call |
| [Library usage](docs/library-usage.md) | Full API reference with examples |
| [Streaming](docs/streaming.md) | WebSocket market-data streams, `IStreamClient`, auto-reconnect |
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

Built by [Orodruin Labs](https://github.com/OrodruinLabs).
