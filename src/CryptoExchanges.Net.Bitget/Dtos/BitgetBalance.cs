namespace CryptoExchanges.Net.Bitget.Services;

internal sealed record BitgetBalance
{
    [JsonPropertyName("coin")]
    public string Coin { get; init; } = string.Empty;

    /// <summary>Available (free) balance for the coin.</summary>
    [JsonPropertyName("available")]
    public string Available { get; init; } = "0";

    /// <summary>Balance frozen in open orders (locked).</summary>
    [JsonPropertyName("frozen")]
    public string Frozen { get; init; } = "0";

    /// <summary>Balance locked for other reasons (e.g. pending settlement); also counts as locked.</summary>
    [JsonPropertyName("locked")]
    public string Locked { get; init; } = "0";
}
