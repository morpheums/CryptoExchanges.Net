namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// The classification result produced by <see cref="IStreamProtocol.Classify"/> for a single
/// received WebSocket frame. The engine uses <see cref="Kind"/> to decide the action and, for
/// <see cref="FrameKind.Data"/> frames, uses <see cref="RoutingKey"/> to find the target subscription.
/// </summary>
/// <param name="Kind">The classified kind of the received frame.</param>
/// <param name="RoutingKey">
/// The routing key that identifies which subscription receives this data frame.
/// <see langword="null"/> for non-data frames (<see cref="FrameKind.Ack"/>,
/// <see cref="FrameKind.Pong"/>, <see cref="FrameKind.Error"/>).
/// </param>
internal readonly record struct StreamFrame(FrameKind Kind, string? RoutingKey);
