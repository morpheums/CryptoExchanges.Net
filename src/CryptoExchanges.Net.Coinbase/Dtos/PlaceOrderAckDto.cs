namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>POST /api/v3/brokerage/orders</c> response. <c>success_response.order_id</c> carries the new order id on success.</summary>
internal sealed record PlaceOrderAckDto
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("success_response")]
    public PlaceOrderSuccessDto? SuccessResponse { get; init; }

    [JsonPropertyName("error_response")]
    public PlaceOrderRejectionDto? ErrorResponse { get; init; }
}
