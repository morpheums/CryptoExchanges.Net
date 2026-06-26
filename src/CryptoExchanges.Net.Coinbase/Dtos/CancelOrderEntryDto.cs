namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>Per-order entry from the batch cancel endpoint.</summary>
internal sealed record CancelOrderEntryDto
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }
}
