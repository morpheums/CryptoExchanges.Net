namespace CryptoExchanges.Net.Okx.Services;

/// <summary>A single account snapshot carrying its per-currency balance details.</summary>
internal sealed record AccountDto
{
    [JsonPropertyName("details")]
    public List<BalanceDto> Details { get; init; } = [];
}
