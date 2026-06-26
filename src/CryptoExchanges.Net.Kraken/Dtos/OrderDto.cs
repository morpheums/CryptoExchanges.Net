namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>A Kraken order record as returned by <c>/0/private/QueryOrders</c> and order lists.</summary>
internal sealed record OrderDto
{
    /// <summary>Asset pair (wsname form, e.g. XBT/USDT).</summary>
    [JsonPropertyName("descr")]
    public OrderDescrDto Descr { get; init; } = new();

    /// <summary>Status: <c>pending</c>, <c>open</c>, <c>closed</c>, <c>canceled</c>, <c>expired</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "open";

    /// <summary>Unix timestamp of order opening.</summary>
    [JsonPropertyName("opentm")]
    public decimal OpenTime { get; init; }

    /// <summary>Unix timestamp of order closing (0 if still open).</summary>
    [JsonPropertyName("closetm")]
    public decimal CloseTime { get; init; }

    /// <summary>Order volume in base currency.</summary>
    [JsonPropertyName("vol")]
    public string Vol { get; init; } = "0";

    /// <summary>Volume executed in base currency.</summary>
    [JsonPropertyName("vol_exec")]
    public string VolExec { get; init; } = "0";

    /// <summary>Total cost (quote currency).</summary>
    [JsonPropertyName("cost")]
    public string Cost { get; init; } = "0";

    /// <summary>Average price of executed volume.</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>User-supplied client order id (optional).</summary>
    [JsonPropertyName("userref")]
    public int? UserRef { get; init; }
}
