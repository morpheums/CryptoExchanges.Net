namespace CryptoExchanges.Net.Core.Models;

/// <summary>
/// A trading pair, e.g. BTC/USDT. Holds typed base and quote <see cref="Asset"/>s.
/// Has no wire format — converting to/from an exchange string is the job of an
/// <see cref="Interfaces.ISymbolMapper"/>.
/// </summary>
public readonly record struct Symbol
{
    /// <summary>The base asset (what you are buying/selling), e.g. BTC.</summary>
    public Asset Base { get; }

    /// <summary>The quote asset (what it is priced in), e.g. USDT.</summary>
    public Asset Quote { get; }

    /// <summary>Creates a trading pair from two distinct, valid assets.</summary>
    /// <exception cref="ArgumentException">Either leg is <see cref="Asset.None"/>, or both legs are equal.</exception>
    public Symbol(Asset @base, Asset quote)
    {
        if (@base.IsNone || quote.IsNone)
            throw new ArgumentException("Symbol legs must be valid assets (not Asset.None).");
        if (@base == quote)
            throw new ArgumentException($"Base and quote must differ (both were '{@base}').");
        Base = @base;
        Quote = quote;
    }

    /// <summary>Human-readable form, e.g. "BTC/USDT". NOT a wire format — do not send to an exchange.</summary>
    public override string ToString() => $"{Base}/{Quote}";
}
