namespace CryptoExchanges.Net.Binance.Services;

internal sealed record AccountDto
{
    [JsonPropertyName("balances")]
    public List<BalanceDto> Balances { get; init; } = [];
}
