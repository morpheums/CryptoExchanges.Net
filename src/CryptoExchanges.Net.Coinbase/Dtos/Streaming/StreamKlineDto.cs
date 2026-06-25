namespace CryptoExchanges.Net.Coinbase.Dtos.Streaming;

/// <summary>
/// One element of the <c>candles</c> array within a Coinbase WebSocket candles event
/// (<c>channel: candles</c>). Timestamps are unix seconds (string-encoded).
/// The <c>candles</c> channel always delivers 1-minute intervals on the
/// Coinbase Advanced Trade WebSocket.
/// </summary>
internal sealed record StreamKlineDto
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    /// <summary>Open time as unix seconds (string-encoded).</summary>
    [JsonPropertyName("start")]
    public string Start { get; init; } = "0";

    [JsonPropertyName("open")]
    public string Open { get; init; } = "0";

    [JsonPropertyName("high")]
    public string High { get; init; } = "0";

    [JsonPropertyName("low")]
    public string Low { get; init; } = "0";

    [JsonPropertyName("close")]
    public string Close { get; init; } = "0";

    [JsonPropertyName("volume")]
    public string Volume { get; init; } = "0";
}
