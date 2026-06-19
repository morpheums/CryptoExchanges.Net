namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>A V5 <c>result.list</c> wrapper, used by the many list-returning endpoints.</summary>
/// <typeparam name="T">The element type of the list.</typeparam>
internal sealed record ListDto<T>
{
    [JsonPropertyName("list")]
    public List<T> List { get; init; } = [];
}
