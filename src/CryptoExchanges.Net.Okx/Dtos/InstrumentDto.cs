namespace CryptoExchanges.Net.Okx.Services;

internal sealed record InstrumentDto
{
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    [JsonPropertyName("baseCcy")]
    public string BaseCcy { get; init; } = string.Empty;

    [JsonPropertyName("quoteCcy")]
    public string QuoteCcy { get; init; } = string.Empty;
}
