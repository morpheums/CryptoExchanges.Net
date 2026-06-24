namespace CryptoExchanges.Net.Okx.Dtos.Streaming;

/// <summary>
/// One element of the <c>data</c> array in an OKX WebSocket ticker frame (<c>channel: tickers</c>).
/// Symbol is sourced from <c>arg.instId</c> on the outer envelope.
/// </summary>
internal sealed record StreamTickerDto
{
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    [JsonPropertyName("last")]
    public string Last { get; init; } = "0";

    [JsonPropertyName("high24h")]
    public string High24h { get; init; } = "0";

    [JsonPropertyName("low24h")]
    public string Low24h { get; init; } = "0";

    [JsonPropertyName("vol24h")]
    public string Vol24h { get; init; } = "0";

    [JsonPropertyName("bidPx")]
    public string BidPx { get; init; } = "0";

    [JsonPropertyName("askPx")]
    public string AskPx { get; init; } = "0";

    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
