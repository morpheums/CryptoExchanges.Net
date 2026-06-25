namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>POST /api/v3/brokerage/orders</c> response. <c>success_response.order_id</c> carries the new order id on success.</summary>
internal sealed record PlaceOrderResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("success_response")]
    public PlaceOrderSuccessDto? SuccessResponse { get; init; }

    [JsonPropertyName("error_response")]
    public PlaceOrderErrorDto? ErrorResponse { get; init; }
}

/// <summary>The nested success object inside a successful place-order response.</summary>
internal sealed record PlaceOrderSuccessDto
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("client_order_id")]
    public string ClientOrderId { get; init; } = string.Empty;
}

/// <summary>The nested error object inside a failed place-order response.</summary>
internal sealed record PlaceOrderErrorDto
{
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("error_details")]
    public string ErrorDetails { get; init; } = string.Empty;
}
