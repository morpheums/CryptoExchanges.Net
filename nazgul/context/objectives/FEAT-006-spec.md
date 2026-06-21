# FEAT-006 — KuCoin Exchange Integration (full parity: REST + WebSocket streaming)

## Objective
Add KuCoin as the 5th exchange at full parity with the existing implementations: REST market
data + account + trading (KC-API passphrase-v2 HMAC signing, bespoke `ISymbolMapper`,
DeltaMapper DTO→model, `AddKucoinExchange` DI, MCP tool wiring) **plus** public WebSocket
streaming (ticker / trade / order book / kline) with auto-reconnect + auto-resubscribe. This
objective also generalizes the shared streaming endpoint seam to support KuCoin's
token-negotiated connection, while keeping Binance's static-URL path working unchanged.

## Objective type
Feature (brownfield extension — clones the verified exchange template; one targeted change to the
shared streaming engine's endpoint seam).

## Reference pattern (read before planning)
- **Closest REST sibling**: OKX/Bitget — both use a passphrase (`ExchangeCredentials.Passphrase`)
  and base64 signatures. Clone their package layout, `DelegatingHandler` signing, retry-only-on-GET,
  per-attempt re-sign (mark-and-strip), and bespoke `ISymbolMapper`.
- **Streaming sibling**: Binance (`src/CryptoExchanges.Net.Binance/Streaming/`) —
  `BinanceStreamProtocol : IStreamProtocol`, per-stream decode closures, `BinanceStreamOptions`,
  `AddBinanceStreams()`. The generic engine/client/factory live under
  `src/CryptoExchanges.Net.Http/Streaming/` and stay exchange-agnostic.
- **House rules**: one-type-per-file; internal `{Concept}Dto` wire DTOs in `Dtos/`; DeltaMapper
  mandate; lean comments; XML docs on interfaces + `<inheritdoc/>` on impls; per-exchange
  `AddKucoinExchange` (ADR-001); TreatWarningsAsErrors + AnalysisLevel=latest-all.

## Scope — In
### REST
- New `CryptoExchanges.Net.Kucoin` project (Core → Http → Exchange → DI layering preserved).
- Market data: tickers, order book, candlesticks, recent trades, exchange info, price,
  symbol resolve / `IsSupported`, ping — matching the `IExchangeClient`/market surface the other
  four exchanges expose.
- Account: balances (per-asset + all).
- Trading: place / cancel / cancel-by-client-id / cancel-all / get / open / history orders, plus
  fills / trade history.
- Symbol mapping: bespoke `ISymbolMapper` for KuCoin's `BASE-QUOTE` dash format (e.g. `BTC-USDT`).
- Mapping: DTO→model via DeltaMapper profiles; internal `{Concept}Dto` wire DTOs in `Dtos/`.

### Signing
- KuCoin `KC-API` scheme: HMAC-SHA256 over `timestamp + method + endpoint + body`, **base64**
  signature; **passphrase-v2** (the passphrase is itself HMAC-SHA256-signed with the secret and
  base64-encoded), `KC-API-KEY-VERSION: 2`. Implemented via the `DelegatingHandler` mark-and-strip
  pattern (per-attempt re-sign; retry stays REST-GET-only). Reuses the existing
  `ExchangeCredentials` (passphrase already supported) and `Core.Auth` primitives.

### DI + MCP
- `AddKucoinExchange` keyed registration mirroring the other exchanges (ADR-001).
- Wire KuCoin into the existing MCP 12-tool vocabulary so agents need no changes when switching to it.

### WebSocket streaming (public)
- Four public streams: ticker, trade, order book, kline — via a new
  `KucoinStreamProtocol : IStreamProtocol` + per-stream decode closures + `KucoinStreamOptions` +
  `AddKucoinStreams()`. Auto-reconnect + auto-resubscribe on the generic engine.
- **Streaming endpoint-seam generalization** (the one shared-engine change): KuCoin negotiates its
  connection — POST `/api/v1/bullet-public` returns a connect token, a list of WS server URLs, and
  server-dictated `pingInterval`/`pingTimeout`; the client connects to
  `{wsUrl}?token={token}&connectId={uuid}`, and the token must be re-negotiated on every reconnect.
  Generalize `IStreamProtocol.Endpoint` (today a static `Uri`, a Binance-only assumption) to an
  async per-connection resolution seam (e.g. `ValueTask<StreamConnectionInfo> ResolveConnectionAsync(ct)`
  yielding URL + heartbeat policy), invoked by the engine before each connect. Binance keeps a
  trivial static implementation; its behavior must not change.

## Scope — Out (deferred)
- Spot only — no futures/margin.
- Private/authenticated streams (account/order updates) — public streams only, like streaming v1.
- Order-book maintenance / synchronized local book — raw frames only (v1 boundary).
- Any KuCoin-specific feature beyond the canonical `Core.Models` cross-exchange contract.

## Binding constraints
- Preserve the 4-layer dependency chain; **no `Core.Models`/DeltaMapper under
  `src/CryptoExchanges.Net.Http/`** (constraint K1) — the endpoint-seam change must stay byte/opaque.
- Streaming engine constraints C1/K2/K3 from FEAT-005 still hold: protocol *describes* heartbeat,
  engine *executes* it; reconnect replays the stored subscribe set; socket reconnect is engine
  backoff, separate from the REST Polly pipeline (retry stays REST-GET-only).
- Follow the verified OKX/Bitget REST pattern and Binance streaming pattern exactly.
- No opsec leakage in public artifacts (README/commits/PRs/MCP metadata stay strictly technical).

## Success criteria
- `AddKucoinExchange` resolves a working `IExchangeClient`; all REST methods return canonical
  `Core.Models` against live KuCoin; signed (private) endpoints authenticate with passphrase-v2.
- Public streams deliver live ticker/trade/order-book/kline `Core.Models` and survive a forced
  disconnect via auto-reconnect + token re-negotiation + auto-resubscribe.
- Binance streaming regression-free after the endpoint-seam generalization.
- Build 0W/0E (TreatWarningsAsErrors); full non-integration suite green; new unit tests use an
  injected fake transport / HTTP handler (no network); live integration smokes self-skip without
  connectivity/credentials.
- README KuCoin row → supported (badge + status); MCP docs reflect KuCoin.

## Build approach
Vertical slice, TDD, REST before streaming:
1. Scaffold `CryptoExchanges.Net.Kucoin` (+ Unit/Integration test projects) cloning OKX.
2. KuCoin signing service + signing handler (passphrase-v2, base64) with unit tests.
3. Bespoke `ISymbolMapper` (`BTC-USDT`) + wire DTOs (`Dtos/`) + DeltaMapper profiles.
4. Market-data client methods → account → trading, each DTO→`Core.Models`, with unit tests.
5. `AddKucoinExchange` DI registration + MCP wiring.
6. Generalize the streaming endpoint seam (async `ResolveConnectionAsync`); migrate Binance to the
   new seam with zero behavior change; engine re-negotiates on reconnect — all under existing
   fake-transport tests.
7. `KucoinStreamProtocol` + 4 decoders + `KucoinStreamOptions` + `AddKucoinStreams()`; bullet-public
   negotiation via the resilient HTTP client; client-initiated ping per server interval.
8. Live integration smokes (REST + one streaming) that self-skip without connectivity.
9. Docs: README supported-exchanges row + MCP reference.
