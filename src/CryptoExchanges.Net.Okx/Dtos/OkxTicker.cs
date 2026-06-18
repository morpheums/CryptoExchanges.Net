namespace CryptoExchanges.Net.Okx.Services;

internal sealed record OkxTicker
{
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    [JsonPropertyName("last")]
    public string Last { get; init; } = "0";

    /// <summary>Open price 24h ago (UTC), used as the reference for the 24h change.</summary>
    [JsonPropertyName("open24h")]
    public string Open24h { get; init; } = "0";

    [JsonPropertyName("high24h")]
    public string High24h { get; init; } = "0";

    [JsonPropertyName("low24h")]
    public string Low24h { get; init; } = "0";

    /// <summary>24h trading volume in the base currency.</summary>
    [JsonPropertyName("vol24h")]
    public string Vol24h { get; init; } = "0";

    /// <summary>24h trading volume in the quote currency.</summary>
    [JsonPropertyName("volCcy24h")]
    public string VolCcy24h { get; init; } = "0";

    /// <summary>Ticker timestamp in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
