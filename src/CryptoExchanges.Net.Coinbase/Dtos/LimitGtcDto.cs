namespace CryptoExchanges.Net.Coinbase.Dtos;

internal sealed record LimitGtcDto
{
    [JsonPropertyName("base_size")]
    public string BaseSize { get; init; } = "0";

    [JsonPropertyName("limit_price")]
    public string LimitPrice { get; init; } = "0";

    [JsonPropertyName("post_only")]
    public bool PostOnly { get; init; }
}
