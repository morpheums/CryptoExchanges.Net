namespace CryptoExchanges.Net.Binance.Services;

internal sealed record TradeDto
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("qty")]
    public string Qty { get; init; } = "0";

    [JsonPropertyName("quoteQty")]
    public string QuoteQty { get; init; } = "0";

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("isBuyerMaker")]
    public bool IsBuyerMaker { get; init; }
}
