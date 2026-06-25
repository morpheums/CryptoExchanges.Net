namespace CryptoExchanges.Net.Kraken.Dtos.Streaming;

/// <summary>
/// One element of the <c>data</c> array in a Kraken WS v2 OHLC frame (<c>channel: ohlc</c>).
/// </summary>
internal sealed record StreamKlineDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("open")]
    public decimal Open { get; init; }

    [JsonPropertyName("high")]
    public decimal High { get; init; }

    [JsonPropertyName("low")]
    public decimal Low { get; init; }

    [JsonPropertyName("close")]
    public decimal Close { get; init; }

    [JsonPropertyName("volume")]
    public decimal Volume { get; init; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("interval_begin")]
    public string IntervalBegin { get; init; } = string.Empty;
}
