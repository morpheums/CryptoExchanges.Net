namespace CryptoExchanges.Net.Coinbase.Dtos;

internal sealed record MarketIocDto
{
    [JsonPropertyName("quote_size")]
    public string QuoteSize { get; init; } = "0";

    [JsonPropertyName("base_size")]
    public string BaseSize { get; init; } = "0";
}
