namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken ticker info as returned by <c>/0/public/Ticker</c>.
/// Each field is a positional array: index 0 is today's value, index 1 is last 24h.
/// </summary>
internal sealed record TickerDto
{
    /// <summary>Ask: [price, wholeLotVolume, lotVolume].</summary>
    [JsonPropertyName("a")]
    public List<string> Ask { get; init; } = [];

    /// <summary>Bid: [price, wholeLotVolume, lotVolume].</summary>
    [JsonPropertyName("b")]
    public List<string> Bid { get; init; } = [];

    /// <summary>Last trade closed: [price, lotVolume].</summary>
    [JsonPropertyName("c")]
    public List<string> Close { get; init; } = [];

    /// <summary>Volume: [today, last24h].</summary>
    [JsonPropertyName("v")]
    public List<string> Volume { get; init; } = [];

    /// <summary>Volume-weighted average price: [today, last24h].</summary>
    [JsonPropertyName("p")]
    public List<string> Vwap { get; init; } = [];

    /// <summary>Number of trades: [today, last24h].</summary>
    [JsonPropertyName("t")]
    public List<int> Trades { get; init; } = [];

    /// <summary>Low price: [today, last24h].</summary>
    [JsonPropertyName("l")]
    public List<string> Low { get; init; } = [];

    /// <summary>High price: [today, last24h].</summary>
    [JsonPropertyName("h")]
    public List<string> High { get; init; } = [];

    /// <summary>Opening price of today's 24h period.</summary>
    [JsonPropertyName("o")]
    public string Open { get; init; } = "0";
}
