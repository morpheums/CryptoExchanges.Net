namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>Minimal place-order acknowledgement from <c>/api/v1/orders</c>.</summary>
internal sealed record OrderAckDto
{
    /// <summary>The server-assigned order id.</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;
}
