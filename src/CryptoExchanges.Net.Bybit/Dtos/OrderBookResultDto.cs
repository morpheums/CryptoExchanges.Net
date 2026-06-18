namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record OrderBookResultDto
{
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("b")]
    public List<List<string>> Bids { get; init; } = [];

    [JsonPropertyName("a")]
    public List<List<string>> Asks { get; init; } = [];

    /// <summary>Timestamp the snapshot was generated, in unix milliseconds.</summary>
    [JsonPropertyName("ts")]
    public long Timestamp { get; init; }

    /// <summary>Update id of the snapshot.</summary>
    [JsonPropertyName("u")]
    public long UpdateId { get; init; }
}
