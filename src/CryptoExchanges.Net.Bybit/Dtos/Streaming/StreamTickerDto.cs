namespace CryptoExchanges.Net.Bybit.Dtos.Streaming;

/// <summary>
/// The <c>data</c> payload of a Bybit v5 WebSocket ticker frame
/// (<c>topic: tickers.{symbol}</c>). All numeric fields are string-encoded per the v5 wire format.
/// </summary>
internal sealed record StreamTickerDto
{
    /// <summary>Trading symbol wire string (e.g. <c>"BTCUSDT"</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Last traded price.</summary>
    [JsonPropertyName("lastPrice")]
    public string LastPrice { get; init; } = "0";

    /// <summary>24-hour high price.</summary>
    [JsonPropertyName("highPrice24h")]
    public string HighPrice24h { get; init; } = "0";

    /// <summary>24-hour low price.</summary>
    [JsonPropertyName("lowPrice24h")]
    public string LowPrice24h { get; init; } = "0";

    /// <summary>24-hour base-asset volume.</summary>
    [JsonPropertyName("volume24h")]
    public string Volume24h { get; init; } = "0";

    /// <summary>24-hour quote-asset turnover.</summary>
    [JsonPropertyName("turnover24h")]
    public string Turnover24h { get; init; } = "0";

    /// <summary>Price 24 hours ago (used to compute price change).</summary>
    [JsonPropertyName("prevPrice24h")]
    public string PrevPrice24h { get; init; } = "0";

    /// <summary>
    /// 24-hour price change as a fraction (e.g. <c>"0.01"</c> = +1%).
    /// Multiply by 100 to obtain a percentage.
    /// </summary>
    [JsonPropertyName("price24hPcnt")]
    public string Price24hPcnt { get; init; } = "0";
}
