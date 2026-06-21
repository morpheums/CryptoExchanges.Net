namespace CryptoExchanges.Net.Kucoin.Dtos.Streaming;

/// <summary>
/// The <c>data</c> payload of a KuCoin WebSocket kline/candle frame
/// (<c>topic: /market/candles:{symbol}_{interval}</c>). The payload carries the
/// interval string and the OHLCV bar as a string array.
/// </summary>
internal sealed record StreamKlineDto
{
    /// <summary>Trading symbol wire string (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Candle interval string (e.g. <c>"1min"</c>).</summary>
    [JsonPropertyName("candles")]
    public List<string> Candles { get; init; } = [];

    /// <summary>Frame timestamp in unix nanoseconds (JSON number — not used by the decoder).</summary>
    [JsonPropertyName("time")]
    public long Time { get; init; }
}
