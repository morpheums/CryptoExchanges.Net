namespace CryptoExchanges.Net.Kraken.Dtos.Streaming;

/// <summary>
/// One element of the <c>data</c> array in a Kraken WS v2 ticker frame (<c>channel: ticker</c>).
/// The symbol is read from the <c>symbol</c> field carried in this frame payload.
/// </summary>
internal sealed record StreamTickerDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("last")]
    public decimal Last { get; init; }

    [JsonPropertyName("high")]
    public decimal High { get; init; }

    [JsonPropertyName("low")]
    public decimal Low { get; init; }

    [JsonPropertyName("volume")]
    public decimal Volume { get; init; }

    [JsonPropertyName("bid")]
    public decimal Bid { get; init; }

    [JsonPropertyName("ask")]
    public decimal Ask { get; init; }

    [JsonPropertyName("change")]
    public decimal Change { get; init; }

    [JsonPropertyName("change_pct")]
    public decimal ChangePct { get; init; }
}
