namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>
/// The account container returned by the KuCoin account-details endpoint.
/// KuCoin's <c>/api/v1/accounts</c> returns a flat list of <see cref="BalanceDto"/> items
/// rather than a nested object, so this type is used where a single account wrapper is needed
/// (e.g. after deserialization via <see cref="ResponseDto{T}"/>).
/// </summary>
internal sealed record AccountDto
{
    /// <summary>Per-currency balance details for this account.</summary>
    [JsonPropertyName("accounts")]
    public List<BalanceDto> Accounts { get; init; } = [];
}
