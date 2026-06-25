namespace CryptoExchanges.Net.Coinbase.Dtos;

internal sealed record TickerDto
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("price_percent_chg_24h")]
    public string PricePercentChg24h { get; init; } = "0";

    [JsonPropertyName("high_24h")]
    public string High24h { get; init; } = "0";

    [JsonPropertyName("low_24h")]
    public string Low24h { get; init; } = "0";

    [JsonPropertyName("volume_24h")]
    public string Volume24h { get; init; } = "0";

    [JsonPropertyName("volume_24h_usd")]
    public string Volume24hUsd { get; init; } = "0";

    /// <summary>Ticker timestamp in RFC3339 format.</summary>
    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;
}
