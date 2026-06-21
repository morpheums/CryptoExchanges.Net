namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>The <c>data</c> payload of the KuCoin <c>/api/v1/timestamp</c> response.</summary>
internal sealed record ServerTimeDto
{
    /// <summary>Server time in unix milliseconds.</summary>
    [JsonPropertyName("data")]
    public long Data { get; init; }
}
