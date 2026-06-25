namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken per-asset balance as returned by <c>/0/private/Balance</c>.
/// The API returns a flat dictionary keyed by Kraken asset ticker (e.g. XXBT, ZUSD).
/// This DTO represents one entry after the caller flattens the dictionary.
/// </summary>
internal sealed record BalanceDto
{
    /// <summary>Kraken asset ticker (e.g. XXBT, ZUSD).</summary>
    public string Asset { get; init; } = string.Empty;

    /// <summary>Available balance (string-encoded decimal).</summary>
    public string Balance { get; init; } = "0";
}
