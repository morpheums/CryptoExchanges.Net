namespace CryptoExchanges.Net.Bitget.Services;

internal sealed record FillDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("priceAvg")]
    public string PriceAvg { get; init; } = "0";

    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    /// <summary>Liquidity role: <c>maker</c> or <c>taker</c>.</summary>
    [JsonPropertyName("tradeScope")]
    public string TradeScope { get; init; } = "taker";

    /// <summary>Fill time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("cTime")]
    public string CTime { get; init; } = "0";
}
