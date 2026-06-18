namespace CryptoExchanges.Net.Binance.Services;

internal sealed record BinanceSymbolInfo
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("baseAsset")]
    public string BaseAsset { get; init; } = string.Empty;

    [JsonPropertyName("quoteAsset")]
    public string QuoteAsset { get; init; } = string.Empty;

    [JsonPropertyName("orderTypes")]
    public List<string> OrderTypes { get; init; } = [];

    [JsonPropertyName("filters")]
    public List<JsonElement> Filters { get; init; } = [];
}
