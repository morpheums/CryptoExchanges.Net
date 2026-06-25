namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>A single account entry from <c>/api/v3/brokerage/accounts</c>.</summary>
internal sealed record AccountDto
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; init; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("available_balance")]
    public AmountDto AvailableBalance { get; init; } = new();

    [JsonPropertyName("hold")]
    public AmountDto Hold { get; init; } = new();
}
