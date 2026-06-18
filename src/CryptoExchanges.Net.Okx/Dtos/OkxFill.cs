namespace CryptoExchanges.Net.Okx.Services;

internal sealed record OkxFill
{
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;

    [JsonPropertyName("ordId")]
    public string OrdId { get; init; } = string.Empty;

    [JsonPropertyName("fillPx")]
    public string FillPx { get; init; } = "0";

    [JsonPropertyName("fillSz")]
    public string FillSz { get; init; } = "0";

    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    /// <summary>Liquidity taker/maker: <c>M</c> = maker, <c>T</c> = taker.</summary>
    [JsonPropertyName("execType")]
    public string ExecType { get; init; } = "T";

    /// <summary>Fill time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
