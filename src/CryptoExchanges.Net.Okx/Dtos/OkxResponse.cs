namespace CryptoExchanges.Net.Okx.Services;

/// <summary>
/// The uniform OKX V5 response envelope: <c>{ code, msg, data }</c>. A non-zero (string) <c>code</c>
/// never reaches the services — the resilience pipeline's error translator converts such envelopes
/// into typed exceptions — so any envelope deserialized here is already a success (<c>code == "0"</c>).
/// </summary>
/// <typeparam name="T">The element type of the <c>data</c> array for the endpoint.</typeparam>
internal sealed record OkxResponse<T>
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "0";

    [JsonPropertyName("msg")]
    public string Msg { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public List<T> Data { get; init; } = [];
}
