namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>Order description sub-object nested in <see cref="OrderDto"/>.</summary>
internal sealed record OrderDescrDto
{
    /// <summary>Asset pair (wsname form, e.g. XBT/USDT).</summary>
    [JsonPropertyName("pair")]
    public string Pair { get; init; } = string.Empty;

    /// <summary>Side: <c>buy</c> or <c>sell</c>.</summary>
    [JsonPropertyName("type")]
    public string Side { get; init; } = "buy";

    /// <summary>Order type: <c>market</c> or <c>limit</c>.</summary>
    [JsonPropertyName("ordertype")]
    public string OrderType { get; init; } = "limit";

    /// <summary>Primary price (limit price for limit orders; string-encoded).</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";
}
