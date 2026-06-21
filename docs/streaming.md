# Streaming

CryptoExchanges.Net supports real-time market-data streaming via WebSocket.
The streaming layer is **opt-in**: REST-only consumers reference only the REST packages
and pay nothing for socket machinery.

> **Scope** — public market-data streams (ticker, trade, order-book, kline) are available for
> Binance and KuCoin. Private streams and order-book maintenance are not included.

---

## `IStreamClient`

`IStreamClient` is the single entry point for all streaming operations.
It provides four subscribe methods that deliver canonical `Core.Models` values
via awaitable callbacks:

```csharp
public interface IStreamClient : IAsyncDisposable
{
    ExchangeId ExchangeId { get; }

    Task<IStreamSubscription> SubscribeToTickerAsync(
        Symbol symbol, StreamHandlers<Ticker> handlers, CancellationToken ct = default);

    Task<IStreamSubscription> SubscribeToTradesAsync(
        Symbol symbol, StreamHandlers<Trade> handlers, CancellationToken ct = default);

    Task<IStreamSubscription> SubscribeToOrderBookAsync(
        Symbol symbol, int depth, StreamHandlers<OrderBook> handlers, CancellationToken ct = default);

    Task<IStreamSubscription> SubscribeToKlinesAsync(
        Symbol symbol, KlineInterval interval, StreamHandlers<Candlestick> handlers, CancellationToken ct = default);
}
```

Disposing the client closes the underlying connection and all active subscriptions.

---

## `StreamHandlers<T>`

`StreamHandlers<T>` bundles callbacks for a single subscription.
Only `OnUpdate` is required; all lifecycle callbacks are optional:

```csharp
public sealed record StreamHandlers<T>(
    Func<T, ValueTask> OnUpdate,
    Func<ValueTask>?          OnReconnecting = null,
    Func<ValueTask>?          OnReconnected  = null,
    Func<StreamLag, ValueTask>? OnLagged     = null);
```

- **`OnUpdate`** — called for every incoming update.
- **`OnReconnecting`** — called when the engine detects a lost connection and starts reconnecting.
- **`OnReconnected`** — called when the engine has reconnected and resubscribed.
- **`OnLagged`** — called when the subscription's bounded buffer fills and updates are dropped.

---

## `IStreamSubscription` and `State`

Each `SubscribeToXxx` call returns an `IStreamSubscription`.
Disposing the subscription unsubscribes from the stream and releases resources.

```csharp
public interface IStreamSubscription : IAsyncDisposable
{
    StreamConnectionState State { get; }
    bool IsConnected { get; }
}
```

`State` progresses through: `Connecting` → `Live` → `Reconnecting` → `Live` (on reconnect) → `Closed` (on dispose).

---

## Auto-reconnect and auto-resubscribe

The streaming engine reconnects automatically on connection loss using a bounded exponential
backoff. On each successful reconnect, all active subscriptions are replayed from an internal
subscribe set — no consumer code is required for reconnect handling. Calling `DisposeAsync`
on a subscription removes it from the subscribe set so it is not resurrected on reconnect.

---

## Setup with dependency injection

### Binance

Call `AddBinanceExchange` first, then `AddBinanceStreams`:

```csharp
services.AddBinanceExchange(o =>
{
    o.ApiKey    = "your-api-key";
    o.SecretKey = "your-secret-key";
});

services.AddBinanceStreams();  // opt-in — REST-only consumers skip this
```

Then resolve `IStreamClientFactory` and call `GetClient`:

```csharp
var factory = serviceProvider.GetRequiredService<IStreamClientFactory>();
await using var client = factory.GetClient(ExchangeId.Binance);

var subscription = await client.SubscribeToTickerAsync(
    new Symbol(Asset.Btc, Asset.Usdt),
    new StreamHandlers<Ticker>(ticker =>
    {
        Console.WriteLine($"BTC/USDT last: {ticker.LastPrice}");
        return ValueTask.CompletedTask;
    }));

// subscription stays active until disposed
await Task.Delay(TimeSpan.FromMinutes(1));
await subscription.DisposeAsync();
```

### KuCoin

KuCoin uses a token-negotiated WebSocket endpoint (bullet-public). Call `AddKucoinExchange`
first (credentials optional for public streams), then `AddKucoinStreams`:

```csharp
services.AddKucoinExchange(o =>
{
    o.ApiKey     = "your-api-key";    // optional for public streams
    o.SecretKey  = "your-secret-key"; // optional for public streams
    o.Passphrase = "your-passphrase"; // optional for public streams
});

services.AddKucoinStreams();  // opt-in — REST-only consumers skip this
```

Then resolve and use exactly as with Binance:

```csharp
var factory = serviceProvider.GetRequiredService<IStreamClientFactory>();
await using var client = factory.GetClient(ExchangeId.Kucoin);

var subscription = await client.SubscribeToTickerAsync(
    new Symbol(Asset.Btc, Asset.Usdt),
    new StreamHandlers<Ticker>(ticker =>
    {
        Console.WriteLine($"BTC/USDT last: {ticker.LastPrice}");
        return ValueTask.CompletedTask;
    }));

await Task.Delay(TimeSpan.FromMinutes(1));
await subscription.DisposeAsync();
```

Supported stream types: ticker, trade, order book, kline (candlestick). Public streams only;
order-book maintenance is not included.

---

## Usage without dependency injection

Use `StreamClientFactory.Create` directly (Binance shown; the same factory method applies to
KuCoin via the appropriate protocol and decoder registry):

```csharp
var options   = new BinanceStreamOptions();
var protocol  = new BinanceStreamProtocol(options);
var symbolMapper = new SymbolMapper(BinanceSymbolFormat.Instance);
// build mapper + registry as in AddBinanceExchange...
var client = StreamClientFactory.Create(
    ExchangeId.Binance,
    protocol,
    decoderRegistry,
    new StreamEngineOptions(),
    () => new ClientWebSocketConnection(),
    NullLogger.Instance,
    symbolMapper);
```

For most use cases the DI path via `AddBinanceStreams` or `AddKucoinStreams` is simpler.

---

## Design reference

The locked internal design is at
[`docs/superpowers/specs/2026-06-19-websocket-streaming-v1-design.md`](superpowers/specs/2026-06-19-websocket-streaming-v1-design.md)
(local, not committed to the repository).
