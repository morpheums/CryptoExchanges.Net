namespace CryptoExchanges.Net.Bitget.Dtos.Streaming;

/// <summary>
/// A single data element from a Bitget v2 WebSocket ticker frame (<c>channel: ticker</c>).
/// All numeric fields are string-encoded per the v2 wire format.
/// </summary>
internal sealed record StreamTickerDto
{
    /// <summary>Trading symbol wire string (e.g. <c>"BTCUSDT"</c>).</summary>
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    /// <summary>Last traded price.</summary>
    [JsonPropertyName("lastPr")]
    public string LastPr { get; init; } = "0";

    /// <summary>24-hour high price.</summary>
    [JsonPropertyName("high24h")]
    public string High24h { get; init; } = "0";

    /// <summary>24-hour low price.</summary>
    [JsonPropertyName("low24h")]
    public string Low24h { get; init; } = "0";

    /// <summary>24-hour base-asset volume.</summary>
    [JsonPropertyName("baseVolume")]
    public string BaseVolume { get; init; } = "0";

    /// <summary>Best bid price.</summary>
    [JsonPropertyName("bidPr")]
    public string BidPr { get; init; } = "0";

    /// <summary>Best ask price.</summary>
    [JsonPropertyName("askPr")]
    public string AskPr { get; init; } = "0";

    /// <summary>Ticker timestamp (unix milliseconds, string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
