namespace CryptoExchanges.Net.Core.Streaming;

/// <summary>
/// Bundles all awaitable callbacks for a single stream subscription into one record.
/// Only <see cref="OnUpdate"/> is required; lifecycle callbacks are optional.
/// </summary>
/// <typeparam name="T">The <c>Core.Models</c> type delivered by the subscription
/// (e.g. <c>Ticker</c>, <c>Trade</c>, <c>OrderBook</c>, <c>Candlestick</c>).</typeparam>
/// <param name="OnUpdate">
/// Invoked for every update arriving on the stream. Must not be <see langword="null"/>.
/// </param>
/// <param name="OnReconnecting">
/// Optional. Invoked when the engine detects a lost connection and begins a reconnect
/// attempt. The subscription's <see cref="IStreamSubscription.State"/> transitions to
/// <see cref="StreamConnectionState.Reconnecting"/> before this callback is called.
/// </param>
/// <param name="OnReconnected">
/// Optional. Invoked when the engine has successfully reconnected and resubscribed.
/// The subscription's <see cref="IStreamSubscription.State"/> transitions to
/// <see cref="StreamConnectionState.Live"/> before this callback is called.
/// </param>
/// <param name="OnLagged">
/// Optional. Invoked when the subscription's bounded buffer fills and the oldest
/// pending update is dropped. Receives a <see cref="StreamLag"/> with the drop count.
/// The lagged signal is never injected into the <typeparamref name="T"/> update stream
/// and is never thrown as an exception.
/// </param>
public sealed record StreamHandlers<T>(
    Func<T, ValueTask> OnUpdate,
    Func<ValueTask>? OnReconnecting = null,
    Func<ValueTask>? OnReconnected = null,
    Func<StreamLag, ValueTask>? OnLagged = null);
