namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>A single wallet account (e.g. UNIFIED) carrying its per-coin balances.</summary>
internal sealed record BybitWalletAccount
{
    [JsonPropertyName("coin")]
    public List<BybitCoinBalance> Coin { get; init; } = [];
}
