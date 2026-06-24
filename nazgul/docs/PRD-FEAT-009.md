# PRD — FEAT-009: WebSocket Streaming for Bybit, OKX, Bitget

## Problem Statement

Bybit, OKX, and Bitget ship as REST-only clients in this library. Consumers who need
real-time price feeds, trade streams, or order-book updates must fall back to polling,
which is rate-limited, adds latency, and produces stale data. The Binance and KuCoin
clients already deliver live WebSocket streams via a shared engine. The three remaining
exchanges are not plugged into that engine.

## Goals

1. Bybit, OKX, and Bitget each expose all four stream kinds (ticker, trade, order-book L2,
   kline) through the same `IStreamClient` surface consumers already use for Binance/KuCoin.
2. No existing consumer code changes — the DI extension is opt-in; REST-only callers pay
   zero cost.
3. Each exchange ships in its own PR (Bybit first) so the work is reviewable and mergeable
   independently.

## Target Users

Library consumers building real-time trading bots, dashboards, or analytics tools who
need live market data from Bybit, OKX, or Bitget.

## Core Features

| Feature | Description |
|---------|-------------|
| Ticker stream | Rolling 24-hour statistics pushed on change |
| Trade stream | Individual executed trade events |
| Order-book L2 | Per-update depth snapshots/deltas (no local book maintenance) |
| Kline/candle stream | OHLCV bars at a caller-specified interval |
| DI opt-in | `AddBybitStreams()` / `AddOkxStreams()` / `AddBitgetStreams()` extensions |
| Auto-reconnect | Inherited from the shared engine (K3 backoff, K2 replay) |

## Out of Scope

- Authenticated / private streams (account, orders, fills)
- Futures, perpetual swaps, or options instruments
- Local order-book maintenance or snapshot reconstruction
- Any change to `IStreamClient`, `IStreamClientFactory`, or the shared engine
- A new transport or WebSocket abstraction

## Acceptance Criteria (Product Level)

1. After calling `services.AddBybitExchange().AddBybitStreams()`, a consumer can call
   `factory.GetClient(ExchangeId.Bybit).SubscribeTickerAsync(btcUsdt, ...)` and receive
   live `Ticker` updates with no additional configuration.
2. The same four stream kinds work for OKX and Bitget with their respective DI methods.
3. Subscribing to 10+ symbols simultaneously delivers data without reconnect loops
   (the multi-symbol pacing regression validated by the integration smoke test).
4. All tests pass: `dotnet test --filter 'Category!=Integration'` exits 0 before each PR.

## Success Metrics

- Integration smoke tests for all three exchanges each assert at least one `OrderBook`
  update delivered within 30 seconds using a multi-symbol set.
- Zero regressions in the existing Binance and KuCoin streaming test suites.
- Each PR passes all reviewers and merges cleanly to main.
