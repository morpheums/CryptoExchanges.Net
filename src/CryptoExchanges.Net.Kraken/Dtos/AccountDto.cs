namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken account balance container as returned by <c>/0/private/Balance</c>.
/// The raw result is a flat <c>{ asset: balance }</c> dictionary; this DTO holds the
/// per-asset entries after the caller flattens the dictionary into <see cref="BalanceDto"/> records.
/// </summary>
internal sealed record AccountDto
{
    public List<BalanceDto> Balances { get; init; } = [];
}
