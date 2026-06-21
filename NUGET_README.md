![CryptoExchanges.Net](https://raw.githubusercontent.com/OrodruinLabs/CryptoExchanges.Net/main/docs/assets/banner.png)

# CryptoExchanges.Net

**A unified .NET SDK for cryptocurrency exchanges — one typed interface across every exchange, with a read-only MCP server for AI agents.**

[![NuGet](https://img.shields.io/nuget/v/CryptoExchanges.Net.svg)](https://www.nuget.org/packages/CryptoExchanges.Net)
[![License](https://img.shields.io/badge/license-Apache--2.0-green)](https://github.com/OrodruinLabs/CryptoExchanges.Net/blob/main/LICENSE)

REST market data, account, and trading across **Binance, Bybit, OKX, and Bitget** — plus live **WebSocket** market-data streaming (Binance) — all mapped to one canonical model set (`Core.Models`).

## Packages

| Package | Purpose |
|---------|---------|
| `CryptoExchanges.Net` | All-exchanges meta-package — `AddCryptoExchanges()` in one call |
| `CryptoExchanges.Net.Binance` · `.Bybit` · `.Okx` · `.Bitget` | Per-exchange implementations |
| `CryptoExchanges.Net.Core` | Canonical models + interfaces (zero dependencies) |
| `CryptoExchanges.Net.Http` | Shared HTTP/resilience pipeline + streaming engine |
| `CryptoExchanges.Net.Mcp` | Read-only MCP stdio server (`crypto-mcp`) for AI agents |

## Install

```bash
dotnet add package CryptoExchanges.Net.Binance
dotnet add package CryptoExchanges.Net
```

## Documentation

Full guides, architecture, and streaming docs:
**https://github.com/OrodruinLabs/CryptoExchanges.Net**

---

*Exchange names are trademarks of their respective owners. CryptoExchanges.Net is an independent open-source project and is not affiliated with, endorsed by, or sponsored by any listed exchange.*
