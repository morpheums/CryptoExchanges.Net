namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>A single recent trade as returned by <c>/api/v1/market/histories</c>.</summary>
internal sealed record TradeDto
{
    /// <summary>Unique sequence identifier for the trade.</summary>
    [JsonPropertyName("sequence")]
    public string Sequence { get; init; } = string.Empty;

    /// <summary>Trade price.</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Trade size (quantity) in base currency.</summary>
    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>Taker order side: <c>buy</c> or <c>sell</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    /// <summary>Trade time in unix nanoseconds (string-encoded).</summary>
    [JsonPropertyName("time")]
    public string Time { get; init; } = "0";
}
