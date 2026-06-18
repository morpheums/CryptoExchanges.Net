namespace CryptoExchanges.Net.Okx.Services;

/// <summary>A single account snapshot carrying its per-currency balance details.</summary>
internal sealed record OkxBalanceAccount
{
    [JsonPropertyName("details")]
    public List<OkxBalanceDetail> Details { get; init; } = [];
}
