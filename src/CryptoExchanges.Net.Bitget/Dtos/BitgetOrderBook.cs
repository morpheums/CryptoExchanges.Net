namespace CryptoExchanges.Net.Bitget.Services;

internal sealed record BitgetOrderBook
{
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];

    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    /// <summary>Snapshot timestamp in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
