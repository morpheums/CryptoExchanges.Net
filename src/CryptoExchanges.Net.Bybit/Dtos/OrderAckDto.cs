namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>The thin acknowledgement Bybit V5 returns from create/cancel: ids only, not a full order.</summary>
internal sealed record OrderAckDto
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("orderLinkId")]
    public string OrderLinkId { get; init; } = string.Empty;
}
