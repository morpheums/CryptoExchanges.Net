namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>A single KuCoin V2 ticker record as returned by <c>/api/v1/market/stats</c>.</summary>
internal sealed record TickerDto
{
    /// <summary>The trading pair symbol in wire format (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Last traded price.</summary>
    [JsonPropertyName("last")]
    public string Last { get; init; } = "0";

    /// <summary>Opening price 24h ago.</summary>
    [JsonPropertyName("open")]
    public string Open { get; init; } = "0";

    /// <summary>Highest price in the last 24h.</summary>
    [JsonPropertyName("high")]
    public string High { get; init; } = "0";

    /// <summary>Lowest price in the last 24h.</summary>
    [JsonPropertyName("low")]
    public string Low { get; init; } = "0";

    /// <summary>24h trading volume in the base currency.</summary>
    [JsonPropertyName("vol")]
    public string Vol { get; init; } = "0";

    /// <summary>24h trading volume in the quote currency.</summary>
    [JsonPropertyName("volValue")]
    public string VolValue { get; init; } = "0";

    /// <summary>Ticker timestamp in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("time")]
    public string Time { get; init; } = "0";
}
