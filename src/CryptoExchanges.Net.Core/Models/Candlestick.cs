namespace CryptoExchanges.Net.Core.Models;

/// <summary>OHLCV candlestick / kline data for a given interval.</summary>
public sealed record Candlestick(
    DateTimeOffset OpenTime,
    DateTimeOffset? CloseTime = null,
    decimal Open = 0,
    decimal High = 0,
    decimal Low = 0,
    decimal Close = 0,
    decimal Volume = 0,
    decimal? QuoteVolume = null,
    int? TradeCount = null,
    Enums.KlineInterval? Interval = null,
    Symbol? TradingSymbol = null);
