namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The nested success object inside a successful place-order response.</summary>
internal sealed record PlaceOrderSuccessDto
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("client_order_id")]
    public string ClientOrderId { get; init; } = string.Empty;
}
