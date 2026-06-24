# TRD — FEAT-009: WebSocket Streaming for Bybit, OKX, Bitget

## Objective

Add public WebSocket market-data streaming (ticker, trade, order-book L2, kline) to
the three REST-only exchanges — Bybit, OKX, and Bitget — in that priority order.
Each exchange reaches full parity with the existing Binance and KuCoin clients by
cloning the verified shared-streaming pattern. No new transport or engine is introduced.

---

## Shared Seam: What the Engine Already Provides

The engine lives in `CryptoExchanges.Net.Http/Streaming/`. All exchange packages share it
unchanged. Each new exchange supplies only the five variation points listed below.

| Component | File | Role |
|-----------|------|------|
| `StreamEngine` | `Streaming/StreamEngine.cs` | Reconnecting byte-engine; drives one `IWebSocketConnection` |
| `IStreamProtocol` | `Streaming/IStreamProtocol.cs` | Per-venue strategy interface |
| `StreamDecoderRegistry` | `Streaming/StreamDecoderRegistry.cs` | Registry of `Func<ReadOnlyMemory<byte>, object>` closures |
| `StreamServiceRegistration.AddStreams<TOptions>()` | `Streaming/StreamServiceRegistration.cs` | Shared DI body; each exchange calls this once |
| `StreamConnectionInfo` | `Streaming/StreamConnectionInfo.cs` | Endpoint URI + `HeartbeatPolicy` + `MinOutboundInterval` |

**Binding constraints (non-negotiable):**

- **K1**: `CryptoExchanges.Net.Http` never references `Core.Models` or DeltaMapper. Decode closures are opaque `Func<ReadOnlyMemory<byte>, object>` in the exchange package.
- **K2**: Subscribe-set replay on reconnect. `IStreamProtocol.BuildSubscribeBatch` (or per-frame `BuildSubscribe`) is called by the engine during replay — the protocol must produce idempotent subscribe frames.
- **K3**: The engine owns the reconnect backoff loop (not Polly, not the protocol).
- **C1**: Heartbeat execution (timers, watchdog, send) lives in the engine. The protocol only _describes_ the policy via `HeartbeatPolicy` data returned from `ResolveConnectionAsync`.

---

## Per-Exchange Variation Points (What Each New Exchange Must Supply)

Each exchange contributes exactly these five artifacts:

### 1. `XxxStreamOptions` — options class

A public `sealed class` in `Streaming/`:

```
public sealed class BybitStreamOptions
{
    public string StreamBaseUrl { get; set; } = "wss://stream.bybit.com/v5/public/spot";
}
```

One property: the static WS base URL. No credentials (public streams only).

### 2. `XxxStreamProtocol : IStreamProtocol` — sealed internal class in `Streaming/`

Must implement all six members:

| Member | Notes |
|--------|-------|
| `ResolveConnectionAsync(ct)` | For static-URL venues (Bybit, OKX, Bitget): build `StreamConnectionInfo` once in the constructor; return via `ValueTask.FromResult`. No I/O needed. |
| `BuildSubscribe(request)` | Returns one subscribe frame as a UTF-8 string per venue wire format. |
| `BuildUnsubscribe(request)` | Returns one unsubscribe frame. |
| `BuildSubscribeBatch(requests)` | Returns a single batched frame or `null` if unsupported. Engine pre-chunks to `MaxBatchSize=100`. |
| `BuildUnsubscribeBatch(requests)` | Same null/batch contract. |
| `RoutingKeyFor(request)` | Returns the routing key that `Classify` will also produce for the matching data frame. The two must agree. |
| `Classify(frame)` | Classifies raw bytes into `FrameKind.Data/Ack/Pong/Error` + routing key. No I/O, no allocation of managed objects beyond `StreamFrame`. |

`HeartbeatPolicy` is set inside `ResolveConnectionAsync` and returned inside `StreamConnectionInfo`.

### 3. `XxxStreamDecoders` — internal static class in `Streaming/`

Mirrors `BinanceStreamDecoders.Build(IMapper, ISymbolMapper)` exactly:

```
internal static class BybitStreamDecoders
{
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper) { ... }
}
```

Each decoder closure:
1. Deserializes the raw `ReadOnlyMemory<byte>` frame (or its data sub-element) into the internal `{Concept}Dto`.
2. Maps via DeltaMapper (`mapper.Map<TDto, TModel>(dto)`) or hand-maps (for Trade, OrderBook, Kline — see Binance pattern).
3. Returns the `Core.Models` object as `object`.

### 4. Wire DTOs — `Dtos/Streaming/` folder

Internal records following the house rule: `{Concept}Dto` naming, one type per file.

Required files per exchange:

| File | Maps to |
|------|---------|
| `StreamTickerDto.cs` | `Ticker` |
| `StreamTradeDto.cs` | `Trade` |
| `StreamDepthDto.cs` | `OrderBook` |
| `StreamKlineDto.cs` | `Candlestick` |

Vendor vocabulary goes in `[JsonPropertyName]` attributes only — never in type names.

### 5. `StreamServiceCollectionExtensions` — public static class in the exchange root namespace

```csharp
public static IServiceCollection AddBybitStreams(
    this IServiceCollection services,
    Action<BybitStreamOptions>? configure = null) =>
    StreamServiceRegistration.AddStreams<BybitStreamOptions>(
        services,
        ExchangeId.Bybit,
        protocolFactory: sp => { ... },
        decoderRegistryFactory: sp => { ... },
        configure: configure);
```

Must be called _after_ `AddBybitExchange` so the keyed `IMapper` and `ISymbolMapper` are already in the container.

---

## Per-Venue WebSocket Specifics

### Bybit v5 Public Spot

| Property | Value |
|----------|-------|
| Endpoint | `wss://stream.bybit.com/v5/public/spot` |
| Subscribe wire format | `{"req_id":"<uuid>","op":"subscribe","args":["publicTrade.BTCUSDT"]}` |
| Unsubscribe wire format | `{"req_id":"<uuid>","op":"unsubscribe","args":["publicTrade.BTCUSDT"]}` |
| Batch capability | Yes — multiple args in one frame; use `BuildSubscribeBatch` |
| Topic → stream kind | `tickers.<SYM>` = Ticker; `publicTrade.<SYM>` = Trade; `orderbook.<DEPTH>.<SYM>` = OrderBook; `kline.<INTERVAL>.<SYM>` = Kline |
| Ack frame | `{"success":true,"ret_msg":"subscribe","op":"subscribe","req_id":"...","conn_id":"..."}` |
| Data frame | `{"topic":"...","type":"snapshot"/"delta","ts":<ms>,"data":{...}}` |
| Heartbeat | Bybit sends a server-side ping every 20 s; the .NET `ClientWebSocket` auto-pong handles control-frame pongs. Use `HeartbeatDirection.ServerPingClientPong`, `Interval=20s`, `Timeout=60s`. Implementor to confirm from Bybit v5 docs. |
| `MinOutboundInterval` | 100 ms (10 msg/s; Bybit v5 public spot connection limit TBC but conservative matches KuCoin) |
| Order-book topic | `orderbook.50.BTCUSDT` (depth 50 is the practical default; implementor to confirm available depth levels: 1/50/200) |
| Kline intervals | `1` `3` `5` `15` `30` `60` `120` `240` `360` `720` `D` `W` `M` — map to `KlineInterval` enum values |
| Classify routing key | `topic` field value, e.g. `"tickers.BTCUSDT"` |
| Symbol wire format | `BTCUSDT` (no separator; reuse existing `BybitSymbolFormat`) |

**Implementor must confirm from current Bybit v5 WebSocket docs:**
- Exact heartbeat direction and ping interval
- Available orderbook depth levels and the depth value used as default
- Whether `type: "snapshot"` vs `"delta"` affects the data shape for order-book (they do — implementor maps both as flat OrderBook; no local-book maintenance required)

### OKX Public WebSocket

| Property | Value |
|----------|-------|
| Endpoint | `wss://ws.okx.com:8443/ws/v5/public` |
| Subscribe wire format | `{"op":"subscribe","args":[{"channel":"tickers","instId":"BTC-USDT"}]}` |
| Unsubscribe wire format | `{"op":"unsubscribe","args":[{"channel":"tickers","instId":"BTC-USDT"}]}` |
| Batch capability | Yes — multiple arg objects in one frame |
| Channel → stream kind | `tickers` = Ticker; `trades` = Trade; `books5` or `books` = OrderBook; `candle1m` etc. = Kline |
| Ack frame | `{"event":"subscribe","arg":{"channel":"...","instId":"..."},"connId":"..."}` |
| Data frame | `{"arg":{"channel":"...","instId":"..."},"data":[{...}]}` |
| Heartbeat | OKX sends text `"ping"` every 30 s; client must reply with text `"pong"`. Use `HeartbeatDirection.ClientPing`, `PingFormat.Text`, payload `"ping"`, `Interval=25s`, `Timeout=35s`. |
| `MinOutboundInterval` | 100 ms (OKX public limit is ~20 msg/3s; 100 ms is safe with margin) |
| Routing key | Composite key from ack/data frame: `<channel>:<instId>`, e.g. `"tickers:BTC-USDT"` |
| Symbol wire format | `BTC-USDT` (dash separator; reuse existing `OkxSymbolFormat`) |
| Kline channel names | `candle1m` `candle3m` `candle5m` `candle15m` `candle30m` `candle1H` `candle2H` `candle4H` `candle6H` `candle12H` `candle1D` `candle1W` `candle1M` |

**Classify note for OKX:** Incoming frames carry `event` (for ack) or `arg` + `data` (for data frames). Routing key must be reconstructed from `arg.channel + ":" + arg.instId`. The text-ping `"pong"` response from the server is a bare string frame, not JSON — `Classify` must handle that case by checking for the literal `pong` bytes before attempting JSON parse, returning `FrameKind.Pong`.

**Implementor must confirm from current OKX v5 WebSocket docs:**
- Whether `books5` or `books` is the preferred order-book channel (books = full, books5 = top-5)
- Exact ping/pong text and interval

### Bitget Public WebSocket

| Property | Value |
|----------|-------|
| Endpoint | `wss://ws.bitget.com/v2/ws/public` |
| Subscribe wire format | `{"op":"subscribe","args":[{"instType":"SPOT","channel":"ticker","instId":"BTCUSDT"}]}` |
| Unsubscribe wire format | `{"op":"unsubscribe","args":[...]}` |
| Batch capability | Yes — multiple arg objects |
| Channel → stream kind | `ticker` = Ticker; `trade` = Trade; `books5` or `books15` = OrderBook; `candle1m` etc. = Kline |
| Ack frame | `{"event":"subscribe","arg":{...},"code":"0","msg":""}` |
| Data frame | `{"action":"snapshot"/"update","arg":{...},"data":[...]}` |
| Heartbeat | Bitget sends server `"ping"` every 30 s; client must reply with `"pong"`. Use `HeartbeatDirection.ClientPing`, `PingFormat.Text`, payload `"pong"` as the reply. Engine does not send proactive pings — describe as `ServerPingClientPong` so the .NET auto-pong handles it, OR use `ClientPing` with a pong-payload if the docs require a text-frame response. Implementor to confirm. |
| `MinOutboundInterval` | 100 ms |
| Routing key | `<channel>:<instId>`, e.g. `"ticker:BTCUSDT"` |
| Symbol wire format | `BTCUSDT` (no separator; reuse existing `BitgetSymbolFormat`) |
| `instType` | Always `"SPOT"` for this objective |

**Implementor must confirm from current Bitget v2 WebSocket docs:**
- Exact heartbeat direction (is it a control-frame Ping or text "ping"?)
- Available order-book channel names and depth levels
- Kline channel naming convention

---

## File Layout per Exchange

Using Bybit as the example:

```
src/CryptoExchanges.Net.Bybit/
  Streaming/
    BybitStreamOptions.cs          # public sealed class BybitStreamOptions
    BybitStreamProtocol.cs         # internal sealed class BybitStreamProtocol : IStreamProtocol
    BybitStreamDecoders.cs         # internal static class BybitStreamDecoders
  Dtos/
    Streaming/
      StreamTickerDto.cs
      StreamTradeDto.cs
      StreamDepthDto.cs
      StreamKlineDto.cs
  StreamServiceCollectionExtensions.cs  # public static class, AddBybitStreams extension
```

OKX: `CryptoExchanges.Net.Okx/Streaming/` and `Dtos/Streaming/` same structure.
Bitget: `CryptoExchanges.Net.Bitget/Streaming/` and `Dtos/Streaming/` same structure.

---

## Pacing Reference (Binance vs KuCoin vs New)

| Exchange | `MinOutboundInterval` | Batch support |
|----------|-----------------------|---------------|
| Binance | 200 ms (5 msg/s) | Yes (`BuildSubscribeBatch`) |
| KuCoin | 100 ms (10 msg/s) | Yes (topic-joined) |
| Bybit | 100 ms (TBC) | Yes (args array) |
| OKX | 100 ms (TBC) | Yes (args array) |
| Bitget | 100 ms (TBC) | Yes (args array) |

---

## Scope Guardrails

- Spot market only (no futures, no perps, no options).
- Public streams only (no authentication, no private channels).
- Delta callbacks only: the engine routes frames directly to subscribers — no local order-book maintenance or snapshot reconstruction.
- Zero public API change to `IStreamClient` or `IStreamClientFactory`.
- `Http` never references `Core.Models` or DeltaMapper (K1).
- One PR per exchange, merged in order: Bybit → OKX → Bitget. Each PR is self-contained and passes all tests before the next begins.
- No changes to `StreamEngine`, `IStreamProtocol`, `StreamServiceRegistration`, or any shared engine files.

---

## Reference Implementations

Read these before implementing each exchange:

- **Shared engine seam**: `src/CryptoExchanges.Net.Http/Streaming/` (all files)
- **Binance protocol** (static URL, server-ping, batched subscribe): `src/CryptoExchanges.Net.Binance/Streaming/`
- **KuCoin protocol** (client-ping, JSON ping payload, batch by topic join): `src/CryptoExchanges.Net.Kucoin/Streaming/`
- **Binance DI extension**: `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs`
- **KuCoin DI extension**: `src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs`
