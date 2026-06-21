namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>A full KuCoin V1 order record as returned by <c>/api/v1/orders/{orderId}</c> and the order lists.</summary>
internal sealed record OrderDto
{
    /// <summary>Order identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Client-assigned order identifier (empty when not set).</summary>
    [JsonPropertyName("clientOid")]
    public string ClientOid { get; init; } = string.Empty;

    /// <summary>Trading pair in wire format (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Order price; <c>"0"</c> for market orders.</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Original order size in the base currency.</summary>
    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>Accumulated filled size in the base currency.</summary>
    [JsonPropertyName("dealSize")]
    public string DealSize { get; init; } = "0";

    /// <summary>Cumulative filled amount in the quote currency.</summary>
    [JsonPropertyName("dealFunds")]
    public string DealFunds { get; init; } = "0";

    /// <summary>Order side: <c>buy</c> or <c>sell</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    /// <summary>Order type: <c>limit</c> or <c>market</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "limit";

    /// <summary><see langword="true"/> when the order is still resting (open or partially filled); <see langword="false"/> when it has completed or been cancelled.</summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    /// <summary>Whether the order has been cancelled.</summary>
    [JsonPropertyName("cancelExist")]
    public bool CancelExist { get; init; }

    /// <summary>Time in force: <c>GTC</c>, <c>GTT</c>, <c>IOC</c>, or <c>FOK</c>.</summary>
    [JsonPropertyName("timeInForce")]
    public string TimeInForce { get; init; } = "GTC";

    /// <summary>Order creation time in unix milliseconds.</summary>
    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; init; }
}
