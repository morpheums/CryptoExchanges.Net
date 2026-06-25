namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>A single fill from <c>/api/v3/brokerage/orders/historical/fills</c>.</summary>
internal sealed record FillDto
{
    [JsonPropertyName("entry_id")]
    public string EntryId { get; init; } = string.Empty;

    [JsonPropertyName("trade_id")]
    public string TradeId { get; init; } = string.Empty;

    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>Taker side: <c>BUY</c> or <c>SELL</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "BUY";

    /// <summary>Liquidity role: <c>MAKER</c> or <c>TAKER</c>.</summary>
    [JsonPropertyName("liquidity_indicator")]
    public string LiquidityIndicator { get; init; } = "TAKER";

    /// <summary>Fill time in RFC3339 format.</summary>
    [JsonPropertyName("trade_time")]
    public string TradeTime { get; init; } = string.Empty;
}
