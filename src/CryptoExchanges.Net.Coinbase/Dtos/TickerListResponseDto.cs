namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/products</c> response envelope when mapped as price data.</summary>
internal sealed record TickerListResponseDto
{
    [JsonPropertyName("products")]
    public List<TickerDto> Products { get; init; } = [];
}
