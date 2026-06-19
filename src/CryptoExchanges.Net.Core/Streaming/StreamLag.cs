namespace CryptoExchanges.Net.Core.Streaming;

/// <summary>
/// Signals that one or more updates were dropped because the subscription's internal
/// buffer was full. The engine evicts the oldest item (DropOldest) and reports the
/// accumulated drop count via <see cref="StreamHandlers{T}.OnLagged"/>.
/// </summary>
/// <param name="DroppedCount">The number of updates dropped since the last lag signal.</param>
public readonly record struct StreamLag(int DroppedCount);
