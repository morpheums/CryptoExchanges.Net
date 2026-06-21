namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>
/// Paged list payload for KuCoin endpoints that return <c>{ items: [...], totalNum, currentPage, pageSize }</c>.
/// Used as the <c>data</c> element inside <see cref="ResponseDto{T}"/> for paginated endpoints
/// (e.g. <c>/api/v1/orders</c>, <c>/api/v1/fills</c>).
/// </summary>
/// <typeparam name="T">The element type of the <c>items</c> array.</typeparam>
internal sealed record ListDto<T>
{
    /// <summary>List of items on the current page.</summary>
    [JsonPropertyName("items")]
    public List<T> Items { get; init; } = [];

    /// <summary>Total number of items across all pages.</summary>
    [JsonPropertyName("totalNum")]
    public int TotalNum { get; init; }

    /// <summary>Current page number (1-based).</summary>
    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; init; }

    /// <summary>Number of items per page.</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}
