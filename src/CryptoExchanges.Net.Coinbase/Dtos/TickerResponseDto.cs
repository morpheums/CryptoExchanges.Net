namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/products/{product_id}/ticker</c> response envelope.</summary>
internal sealed record TickerResponseDto
{
    [JsonPropertyName("trades")]
    public List<TradeDto> Trades { get; init; } = [];

    [JsonPropertyName("best_bid")]
    public string BestBid { get; init; } = "0";

    [JsonPropertyName("best_ask")]
    public string BestAsk { get; init; } = "0";
}
