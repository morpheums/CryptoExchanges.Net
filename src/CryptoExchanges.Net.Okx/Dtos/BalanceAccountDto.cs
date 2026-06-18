namespace CryptoExchanges.Net.Okx.Services;

/// <summary>A single account snapshot carrying its per-currency balance details.</summary>
internal sealed record BalanceAccountDto
{
    [JsonPropertyName("details")]
    public List<BalanceDetailDto> Details { get; init; } = [];
}
