namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>A full Bybit V5 order record as returned by <c>/v5/order/realtime</c> and <c>/v5/order/history</c>.</summary>
internal sealed record BybitOrder
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("orderLinkId")]
    public string OrderLinkId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("qty")]
    public string Qty { get; init; } = "0";

    [JsonPropertyName("cumExecQty")]
    public string CumExecQty { get; init; } = "0";

    /// <summary>Cumulative executed value in the quote asset.</summary>
    [JsonPropertyName("cumExecValue")]
    public string CumExecValue { get; init; } = "0";

    [JsonPropertyName("side")]
    public string Side { get; init; } = "Buy";

    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = "Limit";

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = "New";

    [JsonPropertyName("timeInForce")]
    public string TimeInForce { get; init; } = "GTC";

    [JsonPropertyName("triggerPrice")]
    public string TriggerPrice { get; init; } = "0";

    /// <summary>Order creation time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("createdTime")]
    public string CreatedTime { get; init; } = "0";

    /// <summary>Last update time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("updatedTime")]
    public string UpdatedTime { get; init; } = "0";
}
