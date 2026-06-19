namespace CryptoExchanges.Net.Bybit;

/// <summary>The <c>result</c> shape of the Bybit V5 <c>/v5/market/time</c> response.</summary>
internal sealed record ServerTimeDto
{
    /// <summary>Server time in unix seconds (string-encoded).</summary>
    [JsonPropertyName("timeSecond")]
    public string TimeSecond { get; init; } = "0";

    /// <summary>Server time in unix nanoseconds (string-encoded).</summary>
    [JsonPropertyName("timeNano")]
    public string TimeNano { get; init; } = "0";
}
