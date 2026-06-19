namespace CryptoExchanges.Net.Core.Streaming;

/// <summary>
/// Represents the connection lifecycle state of a stream subscription.
/// </summary>
public enum StreamConnectionState
{
    /// <summary>The subscription is being established for the first time.</summary>
    Connecting,

    /// <summary>The subscription is active and receiving updates.</summary>
    Live,

    /// <summary>The connection was lost and the engine is attempting to reconnect.</summary>
    Reconnecting,

    /// <summary>The subscription has been disposed (unsubscribed) and will receive no further updates.</summary>
    Closed,
}
