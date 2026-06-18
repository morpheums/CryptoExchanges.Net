namespace CryptoExchanges.Net.Binance.Services;

internal sealed record OrderResponseDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("clientOrderId")]
    public string ClientOrderId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("origQty")]
    public string OrigQty { get; init; } = "0";

    [JsonPropertyName("executedQty")]
    public string ExecutedQty { get; init; } = "0";

    /// <summary>Binance 2026: cumulative quote quantity.</summary>
    [JsonPropertyName("cummulativeQuoteQty")]
    public string CumulativeQuoteQty { get; init; } = "0";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "NEW";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "LIMIT";

    [JsonPropertyName("side")]
    public string Side { get; init; } = "BUY";

    [JsonPropertyName("stopPrice")]
    public string StopPrice { get; init; } = "0";

    [JsonPropertyName("timeInForce")]
    public string TimeInForce { get; init; } = "GTC";

    [JsonPropertyName("icebergQty")]
    public string IcebergQty { get; init; } = "0";

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; init; }

    /// <summary>Binance 2026: whether the order is still working.</summary>
    [JsonPropertyName("isWorking")]
    public bool IsWorking { get; init; }

    /// <summary>Binance 2026: working time.</summary>
    [JsonPropertyName("workingTime")]
    public long WorkingTime { get; init; }

    /// <summary>Binance 2026: original quote order quantity.</summary>
    [JsonPropertyName("origQuoteOrderQty")]
    public string OrigQuoteOrderQty { get; init; } = "0";

    /// <summary>Binance 2026: self-trade prevention mode.</summary>
    [JsonPropertyName("selfTradePreventionMode")]
    public string SelfTradePreventionMode { get; init; } = "NONE";
}
