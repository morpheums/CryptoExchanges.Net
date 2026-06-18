namespace CryptoExchanges.Net.Bitget.Services;

/// <summary>Bitget V2 envelope for endpoints whose <c>data</c> is a single object, not an array
/// (e.g. <c>/api/v2/public/time</c>).</summary>
/// <typeparam name="T">The type of the <c>data</c> object.</typeparam>
internal sealed record ObjectResponseDto<T>
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "00000";

    [JsonPropertyName("msg")]
    public string Msg { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; init; }
}
