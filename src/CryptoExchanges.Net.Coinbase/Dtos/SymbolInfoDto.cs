namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>A single product entry from <c>/api/v3/brokerage/products</c>.</summary>
internal sealed record SymbolInfoDto
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    [JsonPropertyName("base_currency_id")]
    public string BaseCurrencyId { get; init; } = string.Empty;

    [JsonPropertyName("quote_currency_id")]
    public string QuoteCurrencyId { get; init; } = string.Empty;

    [JsonPropertyName("base_increment")]
    public string BaseIncrement { get; init; } = "0";

    [JsonPropertyName("quote_increment")]
    public string QuoteIncrement { get; init; } = "0";

    [JsonPropertyName("base_min_size")]
    public string BaseMinSize { get; init; } = "0";

    [JsonPropertyName("base_max_size")]
    public string BaseMaxSize { get; init; } = "0";

    [JsonPropertyName("quote_min_size")]
    public string QuoteMinSize { get; init; } = "0";
}
