namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The Coinbase Advanced Trade <c>/api/v3/brokerage/time</c> response.</summary>
internal sealed record ServerTimeDto
{
    /// <summary>Server epoch time in seconds (ISO 8601 decimal string).</summary>
    [JsonPropertyName("epochSeconds")]
    public string EpochSeconds { get; init; } = "0";

    /// <summary>Server epoch time in milliseconds (ISO 8601 decimal string).</summary>
    [JsonPropertyName("epochMillis")]
    public string EpochMillis { get; init; } = "0";

    /// <summary>Server time in RFC3339 format.</summary>
    [JsonPropertyName("iso")]
    public string Iso { get; init; } = string.Empty;
}
