namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken asset pair entry as returned by <c>/0/public/AssetPairs</c>.
/// The <c>wsname</c> field carries the slash-delimited wsname form (e.g. XBT/USDT),
/// which aligns with <see cref="KrakenSymbolFormat"/>.
/// </summary>
internal sealed record SymbolInfoDto
{
    /// <summary>WebSocket name (slash-delimited, e.g. XBT/USDT). Used as the wire symbol.</summary>
    [JsonPropertyName("wsname")]
    public string Wsname { get; init; } = string.Empty;

    /// <summary>Base asset ticker (Kraken internal form, e.g. XXBT).</summary>
    [JsonPropertyName("base")]
    public string Base { get; init; } = string.Empty;

    /// <summary>Quote asset ticker (Kraken internal form, e.g. ZUSDT).</summary>
    [JsonPropertyName("quote")]
    public string Quote { get; init; } = string.Empty;

    /// <summary>Minimum order size in base currency.</summary>
    [JsonPropertyName("ordermin")]
    public string OrderMin { get; init; } = "0";

    /// <summary>Price decimal places.</summary>
    [JsonPropertyName("pair_decimals")]
    public int PairDecimals { get; init; }

    /// <summary>Lot decimal places.</summary>
    [JsonPropertyName("lot_decimals")]
    public int LotDecimals { get; init; }
}
