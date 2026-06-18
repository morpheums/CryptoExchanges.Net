namespace CryptoExchanges.Net.Okx.Services;

/// <summary>A full OKX V5 order record as returned by <c>/api/v5/trade/order</c> and the order lists.</summary>
internal sealed record OkxOrder
{
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    [JsonPropertyName("ordId")]
    public string OrdId { get; init; } = string.Empty;

    [JsonPropertyName("clOrdId")]
    public string ClOrdId { get; init; } = string.Empty;

    [JsonPropertyName("px")]
    public string Px { get; init; } = "0";

    /// <summary>Original order size (in base currency for spot limit; quote for market-buy by quote).</summary>
    [JsonPropertyName("sz")]
    public string Sz { get; init; } = "0";

    /// <summary>Accumulated filled size in the base currency.</summary>
    [JsonPropertyName("accFillSz")]
    public string AccFillSz { get; init; } = "0";

    /// <summary>Average fill price; OKX emits "" when there are no fills yet.</summary>
    [JsonPropertyName("avgPx")]
    public string AvgPx { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    [JsonPropertyName("ordType")]
    public string OrdType { get; init; } = "limit";

    [JsonPropertyName("state")]
    public string State { get; init; } = "live";

    /// <summary>Order creation time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("cTime")]
    public string CTime { get; init; } = "0";

    /// <summary>Last update time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("uTime")]
    public string UTime { get; init; } = "0";
}
