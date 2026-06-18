namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record BybitTrade
{
    [JsonPropertyName("execId")]
    public string ExecId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>The taker side of the trade (<c>Buy</c>/<c>Sell</c>); a <c>Sell</c> taker means the buyer was the maker.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = "Buy";

    [JsonPropertyName("time")]
    public long Time { get; init; }
}
