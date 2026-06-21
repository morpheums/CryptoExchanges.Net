namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>
/// The uniform KuCoin V1/V2 response envelope: <c>{ code, msg, data }</c>. A non-"200000" <c>code</c>
/// never reaches the services — the resilience pipeline's error translator converts such envelopes
/// into typed exceptions — so any envelope deserialized here is already a success (<c>code == "200000"</c>).
/// </summary>
/// <typeparam name="T">The type of the <c>data</c> payload for the endpoint.</typeparam>
internal sealed record ResponseDto<T>
{
    /// <summary>KuCoin status code; <c>"200000"</c> on success.</summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = "200000";

    /// <summary>Human-readable message accompanying an error code.</summary>
    [JsonPropertyName("msg")]
    public string Msg { get; init; } = string.Empty;

    /// <summary>The response payload.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }
}
