namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>A Coinbase value-with-currency pair used in balance fields.</summary>
internal sealed record AmountDto
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = "0";

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;
}
