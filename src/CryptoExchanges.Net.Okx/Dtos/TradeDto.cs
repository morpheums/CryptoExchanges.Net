namespace CryptoExchanges.Net.Okx.Services;

internal sealed record TradeDto
{
    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;

    [JsonPropertyName("px")]
    public string Px { get; init; } = "0";

    [JsonPropertyName("sz")]
    public string Sz { get; init; } = "0";

    /// <summary>The taker side (<c>buy</c>/<c>sell</c>); a <c>sell</c> taker means the buyer was the maker.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    /// <summary>Trade time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
