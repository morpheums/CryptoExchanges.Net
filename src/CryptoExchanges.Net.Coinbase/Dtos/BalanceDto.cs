namespace CryptoExchanges.Net.Coinbase.Dtos;

internal sealed record BalanceDto
{
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    /// <summary>Available (free) balance for the currency.</summary>
    [JsonPropertyName("available_balance")]
    public AmountDto AvailableBalance { get; init; } = new();

    /// <summary>Balance on hold (locked in open orders).</summary>
    [JsonPropertyName("hold")]
    public AmountDto Hold { get; init; } = new();
}
