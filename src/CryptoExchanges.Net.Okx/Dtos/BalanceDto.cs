namespace CryptoExchanges.Net.Okx.Services;

internal sealed record BalanceDto
{
    [JsonPropertyName("ccy")]
    public string Ccy { get; init; } = string.Empty;

    /// <summary>Available (free) balance for the currency.</summary>
    [JsonPropertyName("availBal")]
    public string AvailBal { get; init; } = "0";

    /// <summary>Balance frozen in open orders / pending settlement (locked).</summary>
    [JsonPropertyName("frozenBal")]
    public string FrozenBal { get; init; } = "0";
}
