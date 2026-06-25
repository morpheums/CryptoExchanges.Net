namespace CryptoExchanges.Net.Kraken.Dtos.Streaming;

/// <summary>A single price level in the Kraken WS v2 order-book snapshot or update.</summary>
internal sealed record BookLevelDto
{
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; init; }
}
