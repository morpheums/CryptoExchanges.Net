namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record CoinBalanceDto
{
    [JsonPropertyName("coin")]
    public string Coin { get; init; } = string.Empty;

    /// <summary>Total wallet balance for the coin (free + locked).</summary>
    [JsonPropertyName("walletBalance")]
    public string WalletBalance { get; init; } = "0";

    /// <summary>Amount locked in open orders / pending settlement.</summary>
    [JsonPropertyName("locked")]
    public string Locked { get; init; } = "0";
}
