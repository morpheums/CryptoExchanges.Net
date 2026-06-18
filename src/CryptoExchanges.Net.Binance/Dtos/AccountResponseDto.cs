namespace CryptoExchanges.Net.Binance.Services;

internal sealed record AccountResponseDto
{
    [JsonPropertyName("balances")]
    public List<BalanceDto> Balances { get; init; } = [];
}
