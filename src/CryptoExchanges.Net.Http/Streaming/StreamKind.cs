namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Discriminates the type of market-data stream a consumer subscribes to.
/// This token is the key used to look up the decode closure in the
/// <see cref="StreamDecoderRegistry"/> and is also the stream-type discriminator
/// carried in <see cref="StreamRequest.Kind"/>.
/// </summary>
internal enum StreamKind
{
    /// <summary>Real-time best-bid/best-ask ticker updates.</summary>
    Ticker,

    /// <summary>Real-time individual trade execution updates.</summary>
    Trade,

    /// <summary>Raw per-frame order-book depth updates (no local-book maintenance in v1).</summary>
    OrderBook,

    /// <summary>OHLCV candlestick / kline updates for a given interval.</summary>
    Kline,
}
