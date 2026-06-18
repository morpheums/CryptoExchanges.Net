namespace CryptoExchanges.Net.Bitget;

/// <summary>The <c>data</c> element of the Bitget V2 <c>/api/v2/public/time</c> response.</summary>
internal sealed record ServerTimeDto
{
    /// <summary>Server time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("serverTime")]
    public string ServerTime { get; init; } = "0";
}
