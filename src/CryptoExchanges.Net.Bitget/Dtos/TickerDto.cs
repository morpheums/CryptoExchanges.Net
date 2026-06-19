namespace CryptoExchanges.Net.Bitget.Services;

internal sealed record TickerDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("lastPr")]
    public string LastPr { get; init; } = "0";

    /// <summary>Opening price 24h ago, the reference for the 24h change.</summary>
    [JsonPropertyName("open")]
    public string Open { get; init; } = "0";

    [JsonPropertyName("high24h")]
    public string High24h { get; init; } = "0";

    [JsonPropertyName("low24h")]
    public string Low24h { get; init; } = "0";

    /// <summary>24h trading volume in the base currency.</summary>
    [JsonPropertyName("baseVolume")]
    public string BaseVolume { get; init; } = "0";

    /// <summary>24h trading volume in the quote currency.</summary>
    [JsonPropertyName("quoteVolume")]
    public string QuoteVolume { get; init; } = "0";

    /// <summary>Fractional 24h price change (e.g. 0.05 = +5%); Bitget reports it directly.</summary>
    [JsonPropertyName("change24h")]
    public string Change24h { get; init; } = "0";

    /// <summary>Ticker timestamp in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
