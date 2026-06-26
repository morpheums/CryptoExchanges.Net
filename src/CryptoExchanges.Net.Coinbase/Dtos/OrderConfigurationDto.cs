namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>Minimally models the nested <c>order_configuration</c> object; covers the most common limit/market spot shapes.</summary>
internal sealed record OrderConfigurationDto
{
    [JsonPropertyName("limit_limit_gtc")]
    public LimitGtcDto? LimitGtc { get; init; }

    [JsonPropertyName("limit_limit_gtd")]
    public LimitGtcDto? LimitGtd { get; init; }

    [JsonPropertyName("market_market_ioc")]
    public MarketIocDto? MarketIoc { get; init; }
}
