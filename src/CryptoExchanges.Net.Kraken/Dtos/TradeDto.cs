namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken recent trade as returned by <c>/0/public/Trades</c>.
/// Kraken returns trades as positional arrays: [price, volume, time, side, orderType, misc, tradeId].
/// This DTO wraps one deserialized row via positional index properties.
/// </summary>
internal sealed record TradeDto
{
    /// <summary>Trade price.</summary>
    [JsonPropertyName("0")]
    public string Price { get; init; } = "0";

    /// <summary>Trade volume.</summary>
    [JsonPropertyName("1")]
    public string Volume { get; init; } = "0";

    /// <summary>Trade time as unix fractional seconds.</summary>
    [JsonPropertyName("2")]
    public decimal Time { get; init; }

    /// <summary>Taker side: <c>b</c> = buy, <c>s</c> = sell.</summary>
    [JsonPropertyName("3")]
    public string Side { get; init; } = "b";

    /// <summary>Order type: <c>m</c> = market, <c>l</c> = limit.</summary>
    [JsonPropertyName("4")]
    public string OrderType { get; init; } = "m";

    /// <summary>Miscellaneous field.</summary>
    [JsonPropertyName("5")]
    public string Misc { get; init; } = string.Empty;

    /// <summary>Trade id (string-encoded integer).</summary>
    [JsonPropertyName("6")]
    public string TradeId { get; init; } = string.Empty;
}
