namespace CryptoExchanges.Net.Coinbase.Dtos.Streaming;

/// <summary>
/// A single price-level update within a Coinbase <c>level2</c> WebSocket event.
/// <c>side</c> is <c>bid</c> or <c>offer</c>; <c>new_quantity</c> of <c>0</c> means removal.
/// </summary>
internal sealed record StreamDepthEntryDto
{
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("price_level")]
    public string PriceLevel { get; init; } = "0";

    [JsonPropertyName("new_quantity")]
    public string NewQuantity { get; init; } = "0";
}
