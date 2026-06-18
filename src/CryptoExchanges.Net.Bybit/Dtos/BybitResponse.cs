namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>
/// The uniform Bybit V5 response envelope: <c>{ retCode, retMsg, result, time }</c>. A non-zero
/// <c>retCode</c> never reaches the services — the resilience pipeline's error translator converts
/// such envelopes into typed exceptions — so any envelope deserialized here is already a success.
/// </summary>
/// <typeparam name="T">The shape of the <c>result</c> object for the endpoint.</typeparam>
internal sealed record BybitResponse<T>
{
    [JsonPropertyName("retCode")]
    public int RetCode { get; init; }

    [JsonPropertyName("retMsg")]
    public string RetMsg { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public T? Result { get; init; }
}
