namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Simple DTO for the /api/v3/time response used by PingAsync.
/// </summary>
internal sealed record ServerTimeResponseDto
{
    [JsonPropertyName("serverTime")]
    public long ServerTime { get; init; }
}
