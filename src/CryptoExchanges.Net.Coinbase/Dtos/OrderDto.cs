namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>
/// An order record from <c>/api/v3/brokerage/orders/historical/batch</c> or the single-order endpoints.
/// The <c>order_configuration</c> nested object carries limit/market specifics; only the fields
/// common across all configuration types are modelled at the top level.
/// </summary>
internal sealed record OrderDto
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("client_order_id")]
    public string ClientOrderId { get; init; } = string.Empty;

    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    /// <summary>Order side: <c>BUY</c> or <c>SELL</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "BUY";

    /// <summary>Order status: <c>OPEN</c>, <c>FILLED</c>, <c>CANCELLED</c>, <c>EXPIRED</c>, <c>FAILED</c>, <c>UNKNOWN_ORDER_STATUS</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "OPEN";

    [JsonPropertyName("order_configuration")]
    public OrderConfigurationDto? OrderConfiguration { get; init; }

    [JsonPropertyName("filled_size")]
    public string FilledSize { get; init; } = "0";

    [JsonPropertyName("average_filled_price")]
    public string AverageFilledPrice { get; init; } = "0";

    [JsonPropertyName("filled_value")]
    public string FilledValue { get; init; } = "0";

    [JsonPropertyName("total_fees")]
    public string TotalFees { get; init; } = "0";

    /// <summary>Order creation time in RFC3339 format.</summary>
    [JsonPropertyName("created_time")]
    public string CreatedTime { get; init; } = string.Empty;
}
