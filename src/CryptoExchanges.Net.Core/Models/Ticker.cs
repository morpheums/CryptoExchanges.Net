namespace CryptoExchanges.Net.Core.Models;

/// <summary>24-hour price change statistics for a trading pair.</summary>
public sealed record Ticker(
    Symbol Symbol,
    decimal LastPrice,
    decimal? OpenPrice = null,
    decimal? HighPrice = null,
    decimal? LowPrice = null,
    decimal? Volume = null,
    decimal? QuoteVolume = null,
    decimal? PriceChange = null,
    decimal? PriceChangePercent = null,
    DateTimeOffset? Timestamp = null);
