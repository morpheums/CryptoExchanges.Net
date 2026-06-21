namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>Per-asset balance leaf as returned within KuCoin's <c>/api/v1/accounts</c> list.</summary>
internal sealed record BalanceDto
{
    /// <summary>Currency ticker (e.g. <c>BTC</c>).</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    /// <summary>Available (free) balance.</summary>
    [JsonPropertyName("available")]
    public string Available { get; init; } = "0";

    /// <summary>Held (frozen/locked) balance in open orders.</summary>
    [JsonPropertyName("holds")]
    public string Holds { get; init; } = "0";
}
