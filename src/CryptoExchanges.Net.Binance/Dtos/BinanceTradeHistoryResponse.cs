namespace CryptoExchanges.Net.Binance.Services;

internal sealed record BinanceTradeHistoryResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("qty")]
    public string Qty { get; init; } = "0";

    [JsonPropertyName("quoteQty")]
    public string QuoteQty { get; init; } = "0";

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("isBuyer")]
    public bool IsBuyer { get; init; }
}
