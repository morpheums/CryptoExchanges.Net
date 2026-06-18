namespace CryptoExchanges.Net.Bitget.Services;

internal sealed record SymbolDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("baseCoin")]
    public string BaseCoin { get; init; } = string.Empty;

    [JsonPropertyName("quoteCoin")]
    public string QuoteCoin { get; init; } = string.Empty;
}
