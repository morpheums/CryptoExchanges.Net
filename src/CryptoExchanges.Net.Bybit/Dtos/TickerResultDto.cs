namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record TickerResultDto
{
    [JsonPropertyName("list")]
    public List<TickerDto> List { get; init; } = [];
}
