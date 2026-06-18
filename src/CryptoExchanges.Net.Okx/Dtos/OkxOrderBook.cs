namespace CryptoExchanges.Net.Okx.Services;

internal sealed record OkxOrderBook
{
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];

    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    /// <summary>Snapshot timestamp in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
