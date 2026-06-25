namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>POST /api/v3/brokerage/orders/batch_cancel</c> response.</summary>
internal sealed record CancelOrdersResponseDto
{
    [JsonPropertyName("results")]
    public List<CancelOrderResultDto> Results { get; init; } = [];
}

/// <summary>Per-order result from the batch cancel endpoint.</summary>
internal sealed record CancelOrderResultDto
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }
}
