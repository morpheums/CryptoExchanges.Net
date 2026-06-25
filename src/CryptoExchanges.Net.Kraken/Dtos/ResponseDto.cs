namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken REST response envelope: <c>{ error:[...], result:{...} }</c>. A non-empty <c>error</c>
/// never reaches the services — the resilience pipeline converts such envelopes into typed exceptions.
/// </summary>
/// <typeparam name="T">The type of the <c>result</c> payload.</typeparam>
internal sealed record ResponseDto<T>
{
    [JsonPropertyName("error")]
    public List<string> Error { get; init; } = [];

    [JsonPropertyName("result")]
    public T? Result { get; init; }
}
