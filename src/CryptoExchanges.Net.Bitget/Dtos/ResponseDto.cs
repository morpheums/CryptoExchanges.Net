namespace CryptoExchanges.Net.Bitget.Services;

/// <summary>
/// The uniform Bitget V2 response envelope: <c>{ code, msg, requestTime, data }</c>. Bitget's success
/// code is the string <c>"00000"</c>; a non-zero code never reaches the services — the resilience
/// pipeline's error translator converts such envelopes into typed exceptions — so any envelope
/// deserialized here is already a success.
/// </summary>
/// <typeparam name="T">The element type of the <c>data</c> array for the endpoint.</typeparam>
internal sealed record ResponseDto<T>
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "00000";

    [JsonPropertyName("msg")]
    public string Msg { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public List<T> Data { get; init; } = [];
}
