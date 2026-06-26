# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

---

## [0.6.0-preview.3] — 2026-06-26

### Fixed

- **Bybit order-book WebSocket stream delivered no updates for non-tier depths** —
  `SubscribeToOrderBookAsync` built the Bybit topic `orderbook.{depth}.{symbol}` from the raw
  requested depth, but Bybit v5 spot only supports depths `1`, `50`, `200`, and `1000`. A depth such
  as `20` produced the invalid topic `orderbook.20.{symbol}`, which Bybit rejected, so no book
  updates were delivered. The requested depth is now mapped up to the nearest supported tier (e.g.
  `20` → `50`); a depth above the maximum `1000` throws `ArgumentOutOfRangeException` rather than
  silently under-delivering.

## [0.6.0-preview.2] — 2026-06-26

### Fixed

- **Coinbase public market data returned HTTP 401 without credentials** — `CoinbaseMarketDataService`
  was calling Coinbase's authenticated Advanced Trade endpoints (`/api/v3/brokerage/products`,
  `/product_book`, `/products/{id}`, `/products/{id}/candles`) for public reads, so credential-less
  consumers got `401 Unauthorized`. Repointed all public market-data reads to Coinbase's public
  `/api/v3/brokerage/market/...` endpoints; `GetRecentTradesAsync` now uses the correct public
  `/market/products/{id}/ticker` path. Account and trading endpoints (which require auth) and
  WebSocket streaming are unchanged.

### Changed

- **Integration smoke tests are now honest** — REST and WebSocket reachability probes skip only on
  genuine connectivity failure (no HTTP response / timeout) and let real HTTP/auth/handshake errors
  fail the run, instead of masking them as "unreachable"; the REST probe now calls a public endpoint
  directly rather than `PingAsync` (which swallowed errors). Coinbase service unit tests now assert
  the exact public, unsigned market-data paths. KuCoin public market-data smoke tests no longer
  require credentials. (Test-only; no library behavior change.)

## [0.6.0-preview.1] — 2026-06-26

### Added

- **Coinbase and Kraken exchange integrations** — two new exchanges at full parity with the existing
  five: REST market data + account + trading, and public WebSocket streaming (ticker / trade /
  order-book L2 / kline) via the existing `IStreamClient` surface. Opt-in per exchange via
  `AddCoinbaseExchange()` / `AddCoinbaseStreams()` and `AddKrakenExchange()` / `AddKrakenStreams()`.
  Two new NuGet packages — `CryptoExchanges.Net.Coinbase` and `CryptoExchanges.Net.Kraken`
  (published set 9 → 11). Coinbase uses per-request ES256 JWT auth; Kraken uses HMAC-SHA512 with a
  nonce. Spot only; public streams only.
- **Connect-time frames engine hook** — `IStreamProtocol.ConnectFrames()` lets a venue send
  connection-level frames on every (re)connect (paced, replayed on reconnect); used to subscribe
  Coinbase's `heartbeats` channel so idle sockets stay alive. Default no-op for all other venues.

### Changed

- **Solution migrated to the `.slnx` format** (replaces `CryptoExchanges.Net.sln`); CI workflows
  updated accordingly.
- **Cross-exchange consistency hardening** — all exchanges now honor `CancellationToken` in
  `IMarketDataService` symbol resolution (`IsSupportedAsync` / `ResolveSymbolAsync`), and their
  WebSocket stream decoders throw a clear decode exception (instead of a `NullReferenceException`)
  on malformed frames.
- **README** — dropped the redundant Status column from the Supported Exchanges table.

## [0.5.0-preview.4] — 2026-06-24

### Added

- **WebSocket streaming for Bybit, OKX, and Bitget** — public spot ticker, trade, order-book (L2),
  and kline streams for the three remaining REST-only exchanges, reaching parity with Binance and
  KuCoin. Opt-in per exchange via `AddBybitStreams()` / `AddOkxStreams()` / `AddBitgetStreams()`,
  exposed through the existing `IStreamClient` surface. Additive public surface only — each exchange
  adds a `StreamOptions` (the namespace carries the exchange) plus its `AddXxxStreams()` extension;
  the shared reconnecting `StreamEngine` is reused unchanged.

### Notes / limitations

- **Trade streams** deliver the most recent trade per frame; when a venue batches multiple trades in
  one push, earlier trades in that frame are not individually delivered (the engine delivers one
  model per frame).
- **OKX kline** channels are served on OKX's separate *business* endpoint
  (`wss://ws.okx.com:8443/ws/v5/business`). Ticker, trade, and order-book use the default public
  endpoint; set `StreamOptions.StreamBaseUrl` to the business URL to receive OKX kline streams.

## [0.5.0-preview.3] — 2026-06-24

### Fixed

- **WebSocket streaming — Binance `ObjectDisposedException` on first order-book subscribe (multi-symbol)**
  A multi-symbol subscribe burst that tripped an early venue close could race the reconnect path:
  `StreamEngine.ReconnectCoreAsync` released its gate before tearing down and re-establishing the
  socket, so the `_socket` field was disposed/reassigned outside the gate while a concurrent
  `SubscribeAsync` was mid-`ConnectAsync` — surfacing as `ObjectDisposedException`
  (`System.Net.WebSockets.ClientWebSocket`) on the first Binance order-book subscription. KuCoin in
  the same process was unaffected.
  Fix: the gate is now held across socket teardown and every reconnect connect attempt (released only
  during the inter-attempt backoff delay); subscribes that arrive mid-reconnect register and defer
  their send to the K2 replay instead of opening a second socket; the reconnect backoff releases its
  gate exactly once; and dispose now awaits the in-flight reconnect task before disposing its
  synchronization primitives. All changes are internal — no public API change.

## [0.5.0-preview.2] — 2026-06-24

### Fixed

- **WebSocket multi-symbol streaming — reconnect loop on subscribe burst (Binance, KuCoin)**
  Subscribing to many symbols sent all control frames in an unpaced burst. Binance rejected the
  burst with a `PolicyViolation` / "Too many requests" close, causing the engine to reconnect and
  immediately replay the full subscribe set as another burst — an infinite reconnect loop. KuCoin
  shared the same unpaced design and was equally affected.
  Fix: every outbound control frame (subscribe, unsubscribe, reconnect-replay, client ping) now
  passes through a per-connection serialized queue with a configurable minimum inter-frame interval
  (`StreamConnectionInfo.MinOutboundInterval`). Reconnect-replay is also batched — Binance packs up
  to 100 symbols per multi-param array frame; KuCoin comma-joins up to 100 topics per frame —
  reducing frame count in addition to pacing each send.

- **Binance combined-stream order-book decoder — updates never reached the callback**
  The Binance combined-stream envelope (`{"stream":"…","data":{…}}`) was being deserialized
  directly into the leaf depth-update DTO instead of unwrapping the `data` field first. All four
  Binance stream types (ticker, trade, order-book, kline) now unwrap the envelope before decoding,
  matching the pattern already used by the KuCoin decoder. Order-book callbacks now fire correctly
  for all symbol counts.

## [0.5.0-preview.1] — 2026-06-21

### Changed

- **[BREAKING — package id / namespace]** Renamed aggregator package
  `CryptoExchanges.Net.DependencyInjection` → `CryptoExchanges.Net`.
  `AddCryptoExchanges` and `CryptoExchangesOptions` moved to namespace `CryptoExchanges.Net`.
  Method name and options shape are unchanged.

### Migration

- Remove: `dotnet add package CryptoExchanges.Net.DependencyInjection`
- Add:    `dotnet add package CryptoExchanges.Net`
- Change: `using CryptoExchanges.Net.DependencyInjection;` → `using CryptoExchanges.Net;`
- `services.AddCryptoExchanges(...)` — unchanged.

### Internal

- Decoupled per-exchange `.Tests.Unit` projects from the aggregator; consolidated
  all-exchanges resolution test into `CryptoExchanges.Net.Tests.Unit`.

---

## [0.4.0-preview.1] - 2026-06-21

### Added

- **KuCoin REST exchange client** — Full parity implementation for spot trading and market data
  - `CryptoExchanges.Net.Kucoin` NuGet package with `AddKucoinExchange()` DI entry (ADR-001 compliant)
  - Market data services: ticker, order book, candlestick, exchange info
  - Trading services: place/cancel orders, view open orders
  - Account services: balances, trade history
  - KC-API passphrase-v2 signing with per-attempt re-signature and retry-on-GET-only strategy
  - Bespoke `ISymbolMapper` for KuCoin's `BTC-USDT` dash-separated format
  - DeltaMapper DTO→`Core.Models` profiles for canonical cross-exchange vocabulary
  - Environment variable credential support (`KUCOIN_API_KEY`, `KUCOIN_SECRET_KEY`, `KUCOIN_PASSPHRASE`)
- **KuCoin WebSocket streaming** — Public market-data streaming via `AddKucoinStreams()`
  - Token-negotiated `bullet-public` connection with auto-reconnect and auto-resubscribe
  - Four decoder pipelines: ticker, trade, order-book (depth), kline (candlestick)
  - Streams deliver canonical `Core.Models` through the same `IStreamClient` interface as Binance
  - Configurable base URL and stream options with fail-fast validation
- **ADR-002: Generalized streaming endpoint seam** — Internal architectural improvement
  - `IStreamProtocol` now features async `ResolveConnectionAsync(ct)` for per-exchange connection resolution
  - Binance migration to the seam with zero behavior change (static URL path unchanged)
  - Enables token-negotiated endpoints (KuCoin) and future dynamic-URL protocols
  - Internal change only (not breaking; `IStreamProtocol` is internal)
- **MCP wiring** — KuCoin surface automatically exposed via MCP server
  - 6 market-data tools (no credentials required)
  - 6 account-scoped tools (read-only API keys)
  - Identical `ToolResult<T>` envelope and error categorization as other exchanges

### Changed

- (No breaking changes in this release)

### Fixed

- (No bug fixes in this release)

---

## [0.3.0-preview.1] - 2026-06-19

### Added

- **WebSocket market-data streaming (v1)** for Binance — live ticker, trade, raw order-book, and kline
  updates delivered as canonical `Core.Models` through `IStreamClient`, with transparent auto-reconnect
  and auto-resubscribe. Opt-in via `AddBinanceStreams`. See `docs/streaming.md`.
  - Shared, exchange-agnostic streaming engine in `CryptoExchanges.Net.Http`: a reconnecting byte
    transport, per-subscription bounded channels with backpressure (drop-oldest + lag signalling), and a
    small per-exchange `IStreamProtocol` + decode seam so additional exchanges add only protocol and decoders.
  - Public REST surface (`IExchangeClient`) is unchanged; streaming is additive and opt-in.
- **Documentation & MCP onboarding overhaul** — comprehensive guides for library usage and MCP server integration
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

[Unreleased]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.6.0-preview.3...HEAD
[0.6.0-preview.3]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.6.0-preview.2...v0.6.0-preview.3
[0.6.0-preview.2]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.6.0-preview.1...v0.6.0-preview.2
[0.6.0-preview.1]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.5.0-preview.4...v0.6.0-preview.1
[0.5.0-preview.4]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.5.0-preview.3...v0.5.0-preview.4
[0.5.0-preview.3]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.5.0-preview.2...v0.5.0-preview.3
[0.5.0-preview.2]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.5.0-preview.1...v0.5.0-preview.2
[0.5.0-preview.1]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.4.0-preview.1...v0.5.0-preview.1
[0.4.0-preview.1]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.3.0-preview.1...v0.4.0-preview.1
[0.3.0-preview.1]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.2.0-preview.1...v0.3.0-preview.1
[0.2.0-preview.1]: https://github.com/OrodruinLabs/CryptoExchanges.Net/compare/v0.1.0-preview.1...v0.2.0-preview.1
[0.1.0-preview.1]: https://github.com/OrodruinLabs/CryptoExchanges.Net/releases/tag/v0.1.0-preview.1
