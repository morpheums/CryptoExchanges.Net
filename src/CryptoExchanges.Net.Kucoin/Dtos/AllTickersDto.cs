namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>Container DTO for the <c>/api/v1/market/allTickers</c> response payload.</summary>
internal sealed record AllTickersDto
{
    /// <summary>Server time in unix milliseconds.</summary>
    [JsonPropertyName("time")]
    public long Time { get; init; }

    /// <summary>List of ticker records.</summary>
    [JsonPropertyName("ticker")]
    public List<TickerDto> Ticker { get; init; } = [];
}
