namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record BybitExecution
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("execId")]
    public string ExecId { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("execPrice")]
    public string ExecPrice { get; init; } = "0";

    [JsonPropertyName("execQty")]
    public string ExecQty { get; init; } = "0";

    [JsonPropertyName("side")]
    public string Side { get; init; } = "Buy";

    /// <summary>Execution time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("execTime")]
    public string ExecTime { get; init; } = "0";

    /// <summary>Whether this fill was the maker side of the trade.</summary>
    [JsonPropertyName("isMaker")]
    public bool IsMaker { get; init; }
}
