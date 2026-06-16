using System.Text.Json.Serialization;

namespace CryptoExchanges.Net.Core.Models;

/// <summary>
/// A crypto asset identified by its ticker (e.g. "BTC", "USDT", "PEPE").
/// Tickers are normalized to upper-case at construction, so equality is ordinal and
/// case-insensitive. The set is intentionally open: any valid ticker is representable.
/// </summary>
[JsonConverter(typeof(AssetJsonConverter))]
public readonly record struct Asset
{
    private const int MaxTickerLength = 32;

    private readonly string? _ticker;

    /// <summary>The normalized (trimmed, upper-cased) ticker. Empty string for <see cref="None"/>.</summary>
    public string Ticker => _ticker ?? string.Empty;

    private Asset(string ticker) => _ticker = ticker;

    /// <summary>The "no asset / unknown" sentinel, equal to <c>default(Asset)</c>.</summary>
    public static Asset None => default;

    /// <summary>True when this is <see cref="None"/> (no ticker).</summary>
    public bool IsNone => string.IsNullOrEmpty(Ticker);

    /// <summary>For <see cref="None"/> returns empty string; otherwise the ticker.</summary>
    public override string ToString() => Ticker;

    /// <summary>
    /// Creates an <see cref="Asset"/> from a ticker. Trims and upper-cases, then validates:
    /// non-empty, length &lt;= 32, characters limited to A-Z and 0-9.
    /// </summary>
    /// <exception cref="ArgumentException">The ticker is empty or contains invalid characters.</exception>
    public static Asset Of(string ticker)
    {
        if (!TryOf(ticker, out var asset))
            throw new ArgumentException($"Invalid asset ticker: '{ticker}'.", nameof(ticker));
        return asset;
    }

    /// <summary>Non-throwing variant of <see cref="Of"/>.</summary>
    public static bool TryOf(string? ticker, out Asset asset)
    {
        asset = None;
        if (string.IsNullOrWhiteSpace(ticker))
            return false;

        var normalized = ticker.Trim().ToUpperInvariant();
        if (normalized.Length is 0 or > MaxTickerLength)
            return false;

        foreach (var c in normalized)
        {
            var ok = c is >= 'A' and <= 'Z' or >= '0' and <= '9';
            if (!ok)
                return false;
        }

        asset = new Asset(normalized);
        return true;
    }
}

/// <summary>Serializes <see cref="Asset"/> as its ticker string.</summary>
public sealed class AssetJsonConverter : System.Text.Json.Serialization.JsonConverter<Asset>
{
    /// <inheritdoc/>
    public override Asset Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        var s = reader.GetString();
        return Asset.TryOf(s, out var asset) ? asset : Asset.None;
    }

    /// <inheritdoc/>
    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        Asset value,
        System.Text.Json.JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Ticker);
    }
}
