# TRD — FEAT-006: KuCoin Exchange Integration

- **Status**: Approved for implementation
- **Date**: 2026-06-20
- **Feature ID**: FEAT-006
- **Type**: Brownfield feature — clones verified exchange template + one shared-engine seam change

## Overview

FEAT-006 adds KuCoin as the 5th exchange and delivers the first exchange with token-negotiated
WebSocket streaming. It requires two separable work streams:

1. **KuCoin package** — REST + DI + streaming, cloning the OKX/Bitget REST pattern and the
   Binance streaming pattern.
2. **Streaming seam generalization** — extend `IStreamProtocol` and `StreamEngine` so the
   connection endpoint is resolved asynchronously per-connect, while leaving Binance unchanged.

---

## Current State (Brownfield Baseline)

The four-layer dependency chain is in place:

```
Core (zero deps) → Http (resilience pipeline) → Exchange (Binance/Bybit/OKX/Bitget) → DI (registration)
```

FEAT-005 shipped a fully functional shared streaming engine under `src/CryptoExchanges.Net.Http/Streaming/`:
- `IStreamProtocol` — per-exchange strategy (static `Uri Endpoint`, subscribe/unsubscribe wire,
  frame classification, heartbeat policy).
- `StreamEngine` — reconnect backoff (K3), replay of subscribe set (K2), heartbeat execution (C1).
- `BinanceStreamProtocol` — static endpoint (`wss://stream.binance.com:9443/stream`).

The `IStreamProtocol.Endpoint` property is today a `Uri` — a synchronous, static value. The engine
calls `_protocol.Endpoint` directly in `OpenSocketAsync` and in the reconnect loop.

---

## Target Architecture

### Work Stream A — Streaming Seam Generalization

**Problem**: KuCoin's WebSocket URL and connect token are returned by `POST /api/v1/bullet-public`
(unauthenticated, JSON body: `{"token":"...", "instanceServers":[{"endpoint":"wss://...","pingInterval":18000,"pingTimeout":10000}]}`).
The token is short-lived; it must be re-negotiated on every reconnect.

**Decision**: See ADR-002 for the full rationale. The change:

1. Add `StreamConnectionInfo` record to `CryptoExchanges.Net.Http.Streaming`:

   ```csharp
   internal sealed record StreamConnectionInfo(Uri Endpoint, HeartbeatPolicy Heartbeat);
   ```

2. Replace `Uri Endpoint { get; }` and `HeartbeatPolicy Heartbeat { get; }` on `IStreamProtocol`
   with a single async method:

   ```csharp
   ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct);
   ```

3. Update `StreamEngine.OpenSocketAsync` and `ReconnectCoreAsync` to `await` the new method
   instead of reading the static property.

4. Migrate `BinanceStreamProtocol` to implement `ResolveConnectionAsync` returning a
   pre-computed `StreamConnectionInfo` (constructed once in the constructor from `BinanceStreamOptions`).
   Remove the now-unused `Endpoint` and `Heartbeat` properties. **Zero behavior change.**

**Constraint K1 preserved**: `StreamConnectionInfo` carries only `Uri` and `HeartbeatPolicy` — no
`Core.Models`, no DeltaMapper reference. The seam stays byte/opaque.

### Work Stream B — CryptoExchanges.Net.Kucoin Package

#### Project Layout (mirrors OKX/Bitget)

```
src/CryptoExchanges.Net.Kucoin/
  Auth/
    KucoinSignatureService.cs      -- HMAC-SHA256 / base64 (sign + passphrase-v2)
  Resilience/
    KucoinSigningHandler.cs        -- DelegatingHandler: mark-and-strip per-attempt re-sign
    KucoinSigningRequest.cs        -- request marker (prevents double-sign on retry)
    KucoinErrorTranslator.cs       -- maps {"code","msg"} → typed exceptions
  Dtos/
    TickerDto.cs / OrderBookDto.cs / CandlestickDto.cs / TradeDto.cs
    ServerTimeDto.cs / SymbolInfoDto.cs / ExchangeInfoDto.cs
    OrderDto.cs / FillDto.cs / BalanceDto.cs / AccountDto.cs
    ResponseDto.cs / ListDto.cs    -- transport envelopes (only wrappers using "Response"/"List")
    Streaming/
      BulletPublicDto.cs           -- bullet-public negotiation response
      StreamTickerDto.cs / StreamTradeDto.cs / StreamDepthDto.cs / StreamKlineDto.cs
  Mapping/
    KucoinMappingProfiles.cs       -- DeltaMapper profiles (DTO → Core.Models)
  Internal/
    KucoinClientComposer.cs        -- single composition root (factory-free + DI)
    KucoinValueParsers.cs          -- decimal/enum parse helpers
    KucoinRequestValidation.cs     -- pre-flight validation
  Services/
    KucoinMarketDataService.cs
    KucoinTradingService.cs
    KucoinAccountService.cs
  Streaming/
    KucoinStreamProtocol.cs        -- IStreamProtocol: ResolveConnectionAsync + subscribe/unsubscribe + Classify
    KucoinStreamDecoders.cs        -- per-stream decode closures (K1: DeltaMapper here, not in Http)
    KucoinStreamOptions.cs         -- RestBaseUrl, StreamPingInterval override
    KucoinBulletPublicClient.cs    -- HTTP POST /api/v1/bullet-public → BulletPublicDto
  KucoinExchangeClient.cs          -- public entry: Create(KucoinOptions) / CreateFromEnvironment()
  KucoinHttpClient.cs / IKucoinHttpClient.cs
  KucoinOptions.cs                 -- BaseUrl, ApiKey, SecretKey, Passphrase, TimeoutSeconds
  KucoinSymbolFormat.cs            -- SymbolFormat(delimiter:"-", casing:Upper) for BTC-USDT
  ServiceCollectionExtensions.cs   -- AddKucoinExchange + AddKucoinStreams
  GlobalUsings.cs
  CryptoExchanges.Net.Kucoin.csproj

tests/CryptoExchanges.Net.Kucoin.Tests.Unit/
tests/CryptoExchanges.Net.Kucoin.Tests.Integration/
```

#### Signing — KC-API Passphrase-v2

KuCoin's signing scheme differs from OKX in two places:

| Aspect | OKX | KuCoin |
|--------|-----|--------|
| Prehash format | `timestamp + METHOD + requestPath + body` | identical |
| Signature encoding | base64 | base64 |
| Passphrase header | `OK-ACCESS-PASSPHRASE` (raw plain text) | `KC-API-PASSPHRASE` (**HMAC-SHA256 signed + base64**) |
| Key-version header | none | `KC-API-KEY-VERSION: 2` |
| Timestamp format | ISO-8601 ms UTC (e.g. `2026-06-17T...Z`) | Unix epoch **milliseconds** (string, e.g. `"1750000000000"`) |

`KucoinSignatureService` exposes:
- `Sign(prehash)` — returns `Convert.ToBase64String(HMAC-SHA256(secret, prehash))`.
- `SignPassphrase(passphrase)` — returns `Convert.ToBase64String(HMAC-SHA256(secret, passphrase))`.
- `static string FormatTimestamp(DateTimeOffset)` — Unix epoch ms as string.
- `static string BuildPrehash(timestamp, method, requestPath, body)`.

`KucoinSigningHandler` strips and re-adds four headers on every attempt:
`KC-API-KEY`, `KC-API-SIGN`, `KC-API-TIMESTAMP`, `KC-API-PASSPHRASE`, `KC-API-KEY-VERSION`.

Retry remains GET-only (same Polly config as all other exchanges).

#### Symbol Mapping

`KucoinSymbolFormat` configures a delimiter of `"-"` and upper casing: `BTC-USDT`.
A bespoke `ISymbolMapper` resolves the registered spot symbols; `IsSupported` reflects availability.

#### DeltaMapper Profiles

`KucoinMappingProfiles` registers one `IMapper` mapping profile with all DTO→model rules.
KuCoin uses decimal-as-string for prices/quantities — `KucoinValueParsers.ParseDecimal` handles
the parse; the profiles call it within mapping closures.

#### `AddKucoinExchange` (ADR-001 compliant)

`ServiceCollectionExtensions.cs` ships in the `CryptoExchanges.Net.Kucoin` assembly itself:
- Registers a named `HttpClient` with the Polly resilience pipeline (retry GET-only, per-attempt timeout).
- Registers `KucoinSigningHandler` as a keyed singleton (signing pipeline).
- Registers `IExchangeClient` (keyed by `ExchangeId.Kucoin`) → `KucoinExchangeClient`.
- `AddKucoinStreams()` registers `IStreamClient` (keyed) → `StreamClient<KucoinStreamProtocol>`.

The `CryptoExchanges.Net.DependencyInjection` convenience package delegates to `AddKucoinExchange`.

#### MCP Wiring

`CryptoExchanges.Net.Mcp` already resolves `IExchangeClient` by `ExchangeId`. Adding
`ExchangeId.Kucoin` to the `Core.Enums` (if not already present) and registering
`AddKucoinExchange` in the MCP host's DI is the only change required. No tool schema changes.

#### WebSocket Streaming — KuCoin Protocol

**Bullet-public negotiation** (`KucoinBulletPublicClient`):

```http
POST https://api.kucoin.com/api/v1/bullet-public
Content-Type: application/json
(no auth headers required)
```

Response:
```json
{
  "code": "200000",
  "data": {
    "token": "...",
    "instanceServers": [
      {
        "endpoint": "wss://ws-api-spot.kucoin.com/",
        "pingInterval": 18000,
        "pingTimeout": 10000
      }
    ]
  }
}
```

`KucoinStreamProtocol.ResolveConnectionAsync(ct)`:
1. Calls `KucoinBulletPublicClient.NegotiateAsync(ct)` → `BulletPublicDto`.
2. Picks the first `instanceServer` endpoint.
3. Appends `?token={token}&connectId={Guid.NewGuid():N}`.
4. Returns `new StreamConnectionInfo(uri, new HeartbeatPolicy(ClientPing, pingInterval, pingTimeout, pingPayload, PingFormat.Json))`.

The ping payload is the KuCoin JSON heartbeat: `{"id":"<timestamp>","type":"ping"}`.

`KucoinStreamProtocol` subscribe/unsubscribe wire format:
```json
{"id":"<timestamp>","type":"subscribe","topic":"/market/ticker:BTC-USDT","privateChannel":false,"response":true}
{"id":"<timestamp>","type":"unsubscribe","topic":"/market/ticker:BTC-USDT","privateChannel":false,"response":true}
```

Topic mappings:
| StreamKind | KuCoin topic |
|------------|-------------|
| Ticker | `/market/ticker:{WIRE_SYMBOL}` |
| Trade | `/market/match:{WIRE_SYMBOL}` |
| OrderBook | `/market/level2:{WIRE_SYMBOL}` (diff snapshot; depth param ignored) |
| Kline | `/market/candles:{WIRE_SYMBOL}_{INTERVAL_WIRE}` |

`Classify` inspects the `"type"` field of incoming frames:
- `"type":"message"` → `FrameKind.Data`; routing key from `"topic"`.
- `"type":"ack"` → `FrameKind.Ack`.
- `"type":"pong"` → `FrameKind.Pong`.
- `"type":"error"` or unknown → `FrameKind.Error`.

`KucoinStreamDecoders` registers four closures (K1: DeltaMapper call inside Kucoin package):
- Ticker: deserialize → `StreamTickerDto` → `mapper.Map<StreamTickerDto, Ticker>`.
- Trade: deserialize → `StreamTradeDto` → hand-map to `Trade` (following Binance convention for trades).
- OrderBook: deserialize → `StreamDepthDto` → hand-map to `OrderBook`.
- Kline: deserialize → `StreamKlineDto` → hand-map to `Candlestick`.

---

## Data Flow (Streaming)

```
Consumer calls SubscribeTickerAsync(symbol, handler)
     |
     v
StreamClient → StreamEngine.SubscribeAsync(request, handlers)
     |
     | (lazy-open: first subscribe)
     v
StreamEngine.OpenSocketAsync()
     |
     v
KucoinStreamProtocol.ResolveConnectionAsync()
     |
     | POST /api/v1/bullet-public → BulletPublicDto
     v
Uri: wss://ws-api-spot.kucoin.com/?token=...&connectId=...
HeartbeatPolicy: ClientPing, interval=18s, timeout=10s, JSON ping
     |
     v
IWebSocketConnection.ConnectAsync(uri)
     |
     v
Engine sends: {"id":"...","type":"subscribe","topic":"/market/ticker:BTC-USDT",...}
     |
     v
PumpLoop: Classify → FrameKind.Data, routingKey="/market/ticker:BTC-USDT"
     → KucoinStreamDecoders[Ticker](bytes) → TickerDto → mapper.Map → Ticker
     → handler(Ticker)
```

On reconnect: engine calls `ResolveConnectionAsync` again — fresh token + URL.

---

## Dependency Impact

| Project | Change |
|---------|--------|
| `CryptoExchanges.Net.Core` | Add `ExchangeId.Kucoin` to `Enums.cs` if not present |
| `CryptoExchanges.Net.Http/Streaming` | `IStreamProtocol`: replace `Endpoint`+`Heartbeat` with `ResolveConnectionAsync`; add `StreamConnectionInfo`; update `StreamEngine` (2 call sites) |
| `CryptoExchanges.Net.Binance/Streaming` | `BinanceStreamProtocol`: implement `ResolveConnectionAsync` returning cached `StreamConnectionInfo`; drop `Endpoint`+`Heartbeat` properties |
| `CryptoExchanges.Net.Kucoin` | New project (all green-field within this brownfield feature) |
| `CryptoExchanges.Net.DependencyInjection` | Add `AddKucoinExchange` delegate call in `AddCryptoExchanges` |
| `CryptoExchanges.Net.Mcp` | Wire `ExchangeId.Kucoin` registration |
| `tests/CryptoExchanges.Net.Kucoin.Tests.Unit` | New test project |
| `tests/CryptoExchanges.Net.Kucoin.Tests.Integration` | New test project |

---

## Build Requirements

- `TreatWarningsAsErrors`: true; `AnalysisLevel`: latest-all; `Nullable`: enable.
- `GenerateDocumentationFile`: true — all public + internal interface members need `<summary>`.
- Implementations use `/// <inheritdoc />` — no repeated doc blocks.
- `CA1031` suppressed per-project where broad catches are justified (same pattern as all existing exchange projects).
- `InternalsVisibleTo` in `CryptoExchanges.Net.Kucoin.csproj` grants access to unit + integration test projects.
