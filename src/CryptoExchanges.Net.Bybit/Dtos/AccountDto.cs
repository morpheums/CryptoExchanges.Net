namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>A single wallet account (e.g. UNIFIED) carrying its per-coin balances.</summary>
internal sealed record AccountDto
{
    [JsonPropertyName("coin")]
    public List<BalanceDto> Coin { get; init; } = [];
}
