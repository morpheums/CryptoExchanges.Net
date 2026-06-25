namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>The <c>result</c> of the Kraken <c>/0/public/Time</c> response.</summary>
internal sealed record ServerTimeDto
{
    /// <summary>Server time as unix seconds.</summary>
    [JsonPropertyName("unixtime")]
    public long UnixTime { get; init; }

    /// <summary>Server time in RFC 1123 format.</summary>
    [JsonPropertyName("rfc1123")]
    public string Rfc1123 { get; init; } = string.Empty;
}
