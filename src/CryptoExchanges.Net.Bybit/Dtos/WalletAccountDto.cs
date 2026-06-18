namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>A single wallet account (e.g. UNIFIED) carrying its per-coin balances.</summary>
internal sealed record WalletAccountDto
{
    [JsonPropertyName("coin")]
    public List<CoinBalanceDto> Coin { get; init; } = [];
}
