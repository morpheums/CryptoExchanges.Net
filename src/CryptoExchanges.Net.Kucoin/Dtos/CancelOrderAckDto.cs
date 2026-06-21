namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>Cancel acknowledgement containing the list of cancelled order ids.</summary>
internal sealed record CancelOrderAckDto
{
    /// <summary>List of cancelled order ids.</summary>
    [JsonPropertyName("cancelledOrderIds")]
    public List<string> CancelledOrderIds { get; init; } = [];
}
