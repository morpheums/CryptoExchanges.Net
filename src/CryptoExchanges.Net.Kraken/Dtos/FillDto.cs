namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>A single executed trade (fill) from Kraken <c>/0/private/TradesHistory</c>.</summary>
internal sealed record FillDto
{
    /// <summary>Asset pair (wsname form, e.g. XBT/USDT).</summary>
    [JsonPropertyName("pair")]
    public string Pair { get; init; } = string.Empty;

    /// <summary>Trade time as unix fractional seconds (string-encoded).</summary>
    [JsonPropertyName("time")]
    public decimal Time { get; init; }

    /// <summary>Order type: <c>market</c> or <c>limit</c>.</summary>
    [JsonPropertyName("ordertype")]
    public string OrderType { get; init; } = "limit";

    /// <summary>Taker side: <c>buy</c> or <c>sell</c>.</summary>
    [JsonPropertyName("type")]
    public string Side { get; init; } = "buy";

    /// <summary>Executed price (string-encoded).</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Executed volume (string-encoded).</summary>
    [JsonPropertyName("vol")]
    public string Volume { get; init; } = "0";

    /// <summary>Order transaction id that generated the fill.</summary>
    [JsonPropertyName("ordertxid")]
    public string OrderTxId { get; init; } = string.Empty;

    /// <summary>Trade id.</summary>
    [JsonPropertyName("postxid")]
    public string PosTxId { get; init; } = string.Empty;

    /// <summary>Maker/taker: <c>m</c> = maker, <c>t</c> = taker.</summary>
    [JsonPropertyName("maker")]
    public bool Maker { get; init; }
}
