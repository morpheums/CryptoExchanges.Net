namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>A single KuCoin trading symbol record as returned by <c>/api/v2/symbols</c>.</summary>
internal sealed record SymbolInfoDto
{
    /// <summary>The trading pair in wire format (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Base currency ticker (e.g. <c>BTC</c>).</summary>
    [JsonPropertyName("baseCurrency")]
    public string BaseCurrency { get; init; } = string.Empty;

    /// <summary>Quote currency ticker (e.g. <c>USDT</c>).</summary>
    [JsonPropertyName("quoteCurrency")]
    public string QuoteCurrency { get; init; } = string.Empty;
}
