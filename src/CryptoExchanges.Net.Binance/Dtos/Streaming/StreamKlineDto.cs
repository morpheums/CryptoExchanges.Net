namespace CryptoExchanges.Net.Binance.Dtos.Streaming;

/// <summary>
/// WebSocket combined-stream kline/candlestick payload (the <c>data</c> field of a
/// <c>stream:&lt;symbol&gt;@kline_&lt;interval&gt;</c> frame).
/// The kline data is nested inside a <c>k</c> object.
/// </summary>
internal sealed record StreamKlineDto
{
    /// <summary>Trading symbol wire string.</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The kline bar data.</summary>
    [JsonPropertyName("k")]
    public StreamKlineBarDto Kline { get; init; } = new();
}
