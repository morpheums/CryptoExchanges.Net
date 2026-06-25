namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken REST response envelope: <c>{ error:[...], result:{...} }</c>. A non-empty <c>error</c>
/// never reaches the services — Kraken returns failures as HTTP 200 + <c>error[]</c>, so
/// <see cref="KrakenHttpClient"/> inspects each response and throws the translated typed exception
/// (the shared non-2xx error pipeline never sees these) before this envelope is deserialized.
/// </summary>
/// <typeparam name="T">The type of the <c>result</c> payload.</typeparam>
internal sealed record ResponseDto<T>
{
    [JsonPropertyName("error")]
    public List<string> Error { get; init; } = [];

    [JsonPropertyName("result")]
    public T? Result { get; init; }
}
