namespace CryptoExchanges.Net.Core.Streaming;

/// <summary>
/// Represents an active stream subscription returned by
/// <see cref="Interfaces.IStreamClient"/>. Disposing the subscription unsubscribes
/// from the stream and releases all associated resources.
/// </summary>
public interface IStreamSubscription : IAsyncDisposable
{
    /// <summary>
    /// The current connection state of this subscription. This is the source of truth
    /// for the subscription lifecycle; transitions are
    /// <see cref="StreamConnectionState.Connecting"/> →
    /// <see cref="StreamConnectionState.Live"/> →
    /// <see cref="StreamConnectionState.Reconnecting"/> →
    /// <see cref="StreamConnectionState.Live"/> (cycle on reconnect) →
    /// <see cref="StreamConnectionState.Closed"/> (on dispose).
    /// </summary>
    StreamConnectionState State { get; }

    /// <summary>
    /// Convenience property. Returns <see langword="true"/> when
    /// <see cref="State"/> is <see cref="StreamConnectionState.Live"/>;
    /// <see langword="false"/> for all other states.
    /// </summary>
    bool IsConnected { get; }
}
