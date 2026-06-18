namespace CryptoExchanges.Net.Okx;

/// <summary>The <c>data</c> element of the OKX V5 <c>/api/v5/public/time</c> response.</summary>
internal sealed record OkxServerTime
{
    /// <summary>Server time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
