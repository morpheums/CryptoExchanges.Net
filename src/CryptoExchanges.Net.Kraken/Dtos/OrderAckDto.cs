namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken <c>AddOrder</c> response payload. Contains the transaction id(s) of the placed order
/// and the human-readable order description echoed from the request.
/// </summary>
internal sealed record OrderAckDto
{
    /// <summary>Transaction id(s) for the placed order(s).</summary>
    [JsonPropertyName("txid")]
    public List<string> TxId { get; init; } = [];

    /// <summary>Order description (echoed back by Kraken).</summary>
    [JsonPropertyName("descr")]
    public OrderDescrDto Descr { get; init; } = new();
}
