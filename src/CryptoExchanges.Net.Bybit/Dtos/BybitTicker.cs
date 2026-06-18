namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record BybitTicker
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("lastPrice")]
    public string LastPrice { get; init; } = "0";

    [JsonPropertyName("prevPrice24h")]
    public string PrevPrice24h { get; init; } = "0";

    [JsonPropertyName("highPrice24h")]
    public string HighPrice24h { get; init; } = "0";

    [JsonPropertyName("lowPrice24h")]
    public string LowPrice24h { get; init; } = "0";

    [JsonPropertyName("volume24h")]
    public string Volume24h { get; init; } = "0";

    [JsonPropertyName("turnover24h")]
    public string Turnover24h { get; init; } = "0";

    /// <summary>24h price change as a fraction (e.g. <c>0.01</c> = +1%); converted to a percent in the profile.</summary>
    [JsonPropertyName("price24hPcnt")]
    public string Price24hPcnt { get; init; } = "0";
}
