using System.ComponentModel.DataAnnotations;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Configuration options that govern the behaviour of the reconnecting byte-engine.
/// All values are validated on container start via <c>ValidateOnStart</c>.
/// </summary>
internal sealed class StreamEngineOptions
{
    /// <summary>
    /// Capacity of each per-subscription bounded channel.
    /// When the channel is full the oldest pending item is evicted (DropOldest) and the
    /// subscription's <c>OnLagged</c> callback is raised with the accumulated drop count.
    /// Defaults to <c>128</c>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ChannelCapacity { get; set; } = 128;

    /// <summary>
    /// After the last active subscription is removed, the engine waits this long before
    /// closing the underlying socket. This avoids reconnect thrash when a consumer briefly
    /// has zero subscriptions (e.g., rotating symbols). Defaults to <c>30 seconds</c>.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]
    public TimeSpan IdleCloseDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initial backoff delay for the reconnect loop. Defaults to <c>1 second</c>.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]
    public TimeSpan BackoffInitial { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum backoff delay cap for the reconnect loop. Defaults to <c>60 seconds</c>.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]
    public TimeSpan BackoffMax { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Multiplier applied to the current backoff delay on each successive reconnect
    /// attempt. Defaults to <c>2.0</c> (exponential doubling).
    /// </summary>
    [Range(1.0, 10.0)]
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Maximum number of reconnect attempts before the engine gives up and transitions
    /// all subscriptions to <see cref="CryptoExchanges.Net.Core.Streaming.StreamConnectionState.Closed"/>.
    /// Zero means unlimited. Defaults to <c>0</c> (unlimited).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxReconnectAttempts { get; set; }
}
