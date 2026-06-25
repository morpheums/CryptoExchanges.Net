namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/accounts</c> response envelope.</summary>
internal sealed record AccountsEnvelopeDto
{
    [JsonPropertyName("accounts")]
    public List<AccountDto> Accounts { get; init; } = [];
}
