namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>An executed fill/trade record as returned by <c>/api/v1/fills</c>.</summary>
internal sealed record FillDto
{
    /// <summary>Trade identifier.</summary>
    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;

    /// <summary>Order identifier associated with this fill.</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Trading pair in wire format (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Fill price.</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Fill size (quantity) in base currency.</summary>
    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>Order side: <c>buy</c> or <c>sell</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    /// <summary>Liquidity role: <c>maker</c> or <c>taker</c>.</summary>
    [JsonPropertyName("liquidity")]
    public string Liquidity { get; init; } = "taker";

    /// <summary>Fill time in unix milliseconds.</summary>
    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; init; }
}
