namespace CryptoExchanges.Net.Coinbase.Dtos.Streaming;

/// <summary>
/// One element of the <c>events</c> array in a Coinbase WebSocket order-book frame
/// (<c>channel: level2</c>). Coinbase uses the <c>level2</c> channel (not <c>level2_batch</c>)
/// because it delivers an initial <c>snapshot</c> type followed by incremental <c>update</c>
/// types, providing a complete order-book view without extra aggregation.
/// Each price-level update includes <c>side</c> (<c>bid</c> or <c>offer</c>),
/// <c>price_level</c>, and <c>new_quantity</c>.
/// </summary>
internal sealed record StreamDepthDto
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    [JsonPropertyName("updates")]
    public List<StreamDepthEntryDto> Updates { get; init; } = [];
}
