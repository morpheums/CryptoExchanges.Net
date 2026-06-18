namespace CryptoExchanges.Net.Core.Models;

/// <summary>Exchange-wide trading rules and symbol information.</summary>
public sealed record ExchangeInfo(
    string ExchangeName,
    IReadOnlyList<SymbolInfo> Symbols,
    IReadOnlyList<RateLimit> RateLimits);
