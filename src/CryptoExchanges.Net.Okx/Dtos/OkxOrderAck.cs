namespace CryptoExchanges.Net.Okx.Services;

/// <summary>
/// The per-order acknowledgement OKX V5 returns from place/cancel: ids + a per-order status code.
/// OKX nests a per-order outcome (<c>sCode</c>/<c>sMsg</c>) inside the data array even when the
/// top-level <c>code</c> is "0"; a non-zero <c>sCode</c> never reaches the services because the
/// resilience pipeline's error translator converts it into a typed exception.
/// </summary>
internal sealed record OkxOrderAck
{
    [JsonPropertyName("ordId")]
    public string OrdId { get; init; } = string.Empty;

    [JsonPropertyName("clOrdId")]
    public string ClOrdId { get; init; } = string.Empty;

    /// <summary>Per-order result code ("0" = success).</summary>
    [JsonPropertyName("sCode")]
    public string SCode { get; init; } = "0";

    [JsonPropertyName("sMsg")]
    public string SMsg { get; init; } = string.Empty;
}
