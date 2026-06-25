namespace CryptoExchanges.Net.Coinbase.Dtos;

internal sealed record TradeDto
{
    [JsonPropertyName("trade_id")]
    public string TradeId { get; init; } = string.Empty;

    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>The taker side: <c>BUY</c> or <c>SELL</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "BUY";

    /// <summary>Trade time in RFC3339 format.</summary>
    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;
}
