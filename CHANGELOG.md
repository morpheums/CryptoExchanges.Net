# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Documentation & MCP onboarding overhaul** — Comprehensive guides for library usage and MCP server integration
  - 7 exchange SVG icons under `docs/assets/exchanges/` for visual brand consistency
  - 4 core library docs: getting started guide, library usage patterns, architecture deep dive, per-exchange details
  - 2 MCP integration docs: MCP server setup and configuration, MCP client integration guide
  - Lean, navigator-friendly README rewrite (97 lines) with exchange icon table and docs index

## [0.2.0-preview.1] - 2026-06-19

### Added

- **MCP Server** (`CryptoExchanges.Net.Mcp`) — Read-only [Model Context Protocol](https://modelcontextprotocol.io) stdio server enabling LLM agent access to crypto exchange data
  - Packaged as a global .NET tool: `dotnet tool install -g CryptoExchanges.Net.Mcp`
  - 12 read-only agent tools across four exchanges (Binance, Bybit, OKX, Bitget)
  - 6 market-data tools (no credentials required): price, ticker, order book, candles, recent trades, exchange info
  - 6 account-scoped tools (read-only API keys): balances, single balance, open orders, order history, single order details, trade history
  - Environment variable credential management for each exchange
  - Structured `ToolResult<T>` response envelope with error categories (`AuthRequired`, `RateLimited`, `Connectivity`, `SymbolNotSupported`, `ExchangeUnavailable`, `BadRequest`, `BadInterval`, `ExchangeError`, `Unknown`)
  - All tools return canonical models — one agent vocabulary works identically across all venues
  - **No trading/write operations** — read-only by design
  - Count parameters (`depth`, `limit`) reject non-positive values with a `BadRequest` tool error instead of forwarding an opaque exchange error

### Changed

- (No breaking changes in this release)

### Fixed

- (No bug fixes in this release)

---

## [0.1.0-preview.1] - 2026-06-01

### Added

- Core abstractions: `IExchangeClient`, `IMarketDataService`, `ITradingService`, `IAccountService`
- Binance REST client implementation
- Bybit REST client implementation
- OKX REST client implementation
- Bitget REST client implementation
- HTTP abstraction layer with unified error handling
- Dependency injection extensions with keyed services for multi-exchange support
- Typed symbols and assets with long-tail support
- Market data operations: tickers, order book, candles, exchange info
- Trading operations: place/cancel orders, get open orders
- Account operations: balances, trade history
- Comprehensive unit and integration test suite

[Unreleased]: https://github.com/morpheums/CryptoExchanges.Net/compare/v0.2.0-preview.1...HEAD
[0.2.0-preview.1]: https://github.com/morpheums/CryptoExchanges.Net/compare/v0.1.0-preview.1...v0.2.0-preview.1
[0.1.0-preview.1]: https://github.com/morpheums/CryptoExchanges.Net/releases/tag/v0.1.0-preview.1
