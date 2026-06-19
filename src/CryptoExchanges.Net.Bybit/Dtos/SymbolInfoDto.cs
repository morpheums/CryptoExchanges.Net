namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record SymbolInfoDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("baseCoin")]
    public string BaseCoin { get; init; } = string.Empty;

    [JsonPropertyName("quoteCoin")]
    public string QuoteCoin { get; init; } = string.Empty;
}
