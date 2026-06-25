namespace CryptoExchanges.Net.Kraken.Dtos.Streaming;

/// <summary>
/// One element of the <c>data</c> array in a Kraken WS v2 order-book frame (<c>channel: book</c>).
/// Each price level is <c>{"price":..., "qty":...}</c>.
/// </summary>
internal sealed record StreamDepthDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("bids")]
    public List<BookLevelDto> Bids { get; init; } = [];

    [JsonPropertyName("asks")]
    public List<BookLevelDto> Asks { get; init; } = [];

    [JsonPropertyName("checksum")]
    public long Checksum { get; init; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;
}

/// <summary>A single price level in the Kraken WS v2 order-book snapshot or update.</summary>
internal sealed record BookLevelDto
{
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; init; }
}
