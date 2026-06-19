namespace CryptoExchanges.Net.Binance.Services;

internal sealed record OrderBookDto
{
    [JsonPropertyName("lastUpdateId")]
    public long LastUpdateId { get; init; }

    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];
}
