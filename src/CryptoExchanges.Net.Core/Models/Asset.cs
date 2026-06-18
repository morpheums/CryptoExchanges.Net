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

    // ── Curated, NON-exhaustive well-known assets (autocomplete for the common path).
    //    The set is open: use Asset.Of("...") for anything not listed here. ──
    /// <summary>Bitcoin (BTC).</summary>
    public static readonly Asset Btc   = Of("BTC");
    /// <summary>Ethereum (ETH).</summary>
    public static readonly Asset Eth   = Of("ETH");
    /// <summary>BNB (BNB).</summary>
    public static readonly Asset Bnb   = Of("BNB");
    /// <summary>Solana (SOL).</summary>
    public static readonly Asset Sol   = Of("SOL");
    /// <summary>XRP (XRP).</summary>
    public static readonly Asset Xrp   = Of("XRP");
    /// <summary>Cardano (ADA).</summary>
    public static readonly Asset Ada   = Of("ADA");
    /// <summary>Dogecoin (DOGE).</summary>
    public static readonly Asset Doge  = Of("DOGE");
    /// <summary>TRON (TRX).</summary>
    public static readonly Asset Trx   = Of("TRX");
    /// <summary>Tether (USDT).</summary>
    public static readonly Asset Usdt  = Of("USDT");
    /// <summary>USD Coin (USDC).</summary>
    public static readonly Asset Usdc  = Of("USDC");
    /// <summary>First Digital USD (FDUSD).</summary>
    public static readonly Asset Fdusd = Of("FDUSD");
    /// <summary>Dai (DAI).</summary>
    public static readonly Asset Dai   = Of("DAI");
    /// <summary>Euro (EUR).</summary>
    public static readonly Asset Eur   = Of("EUR");
    /// <summary>British Pound (GBP).</summary>
    public static readonly Asset Gbp   = Of("GBP");

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
        if (normalized.Length > MaxTickerLength)
            return false;

        foreach (var c in normalized)
        {
            if (c is not (>= 'A' and <= 'Z' or >= '0' and <= '9'))
                return false;
        }

        asset = new Asset(normalized);
        return true;
    }
}
