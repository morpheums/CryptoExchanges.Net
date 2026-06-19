namespace CryptoExchanges.Net.Bitget.Services;

/// <summary>A full Bitget V2 order record as returned by <c>/api/v2/spot/trade/orderInfo</c> and the order lists.</summary>
internal sealed record OrderDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("clientOid")]
    public string ClientOid { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Original order size (base currency for limit; quote for market-buy by quote).</summary>
    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>Accumulated filled base quantity.</summary>
    [JsonPropertyName("baseVolume")]
    public string BaseVolume { get; init; } = "0";

    /// <summary>Accumulated filled quote amount (price * filled base).</summary>
    [JsonPropertyName("quoteVolume")]
    public string QuoteVolume { get; init; } = "0";

    /// <summary>Average fill price; Bitget emits "" / "0" when there are no fills yet.</summary>
    [JsonPropertyName("priceAvg")]
    public string PriceAvg { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = "limit";

    /// <summary>Time-in-force (<c>gtc</c>/<c>ioc</c>/<c>fok</c>/<c>post_only</c>).</summary>
    [JsonPropertyName("force")]
    public string Force { get; init; } = "gtc";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "live";

    /// <summary>Order creation time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("cTime")]
    public string CTime { get; init; } = "0";

    /// <summary>Last update time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("uTime")]
    public string UTime { get; init; } = "0";
}
