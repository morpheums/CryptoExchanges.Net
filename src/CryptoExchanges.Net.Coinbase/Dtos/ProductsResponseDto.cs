namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/products</c> response envelope.</summary>
internal sealed record ProductsResponseDto
{
    [JsonPropertyName("products")]
    public List<SymbolInfoDto> Products { get; init; } = [];
}
