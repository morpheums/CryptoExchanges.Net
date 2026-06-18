namespace CryptoExchanges.Net.Bitget.Services;

/// <summary>
/// The per-order acknowledgement Bitget V2 returns from place/cancel: the ids only. A non-success
/// envelope never reaches the services because the resilience pipeline's error translator converts
/// it into a typed exception.
/// </summary>
internal sealed record BitgetOrderAck
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("clientOid")]
    public string ClientOid { get; init; } = string.Empty;
}
