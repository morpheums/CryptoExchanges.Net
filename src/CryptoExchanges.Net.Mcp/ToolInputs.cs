using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Mcp;

/// <summary>Parses agent-supplied string inputs into the library's typed values.</summary>
public static class ToolInputs
{
    private static readonly Dictionary<string, ExchangeId> Exchanges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["binance"] = ExchangeId.Binance,
            ["bybit"] = ExchangeId.Bybit,
            ["okx"] = ExchangeId.Okx,
            ["bitget"] = ExchangeId.Bitget,
        };

    private static readonly Dictionary<string, KlineInterval> Intervals =
        new(StringComparer.Ordinal)
        {
            ["1m"] = KlineInterval.OneMinute, ["3m"] = KlineInterval.ThreeMinutes,
            ["5m"] = KlineInterval.FiveMinutes, ["15m"] = KlineInterval.FifteenMinutes,
            ["30m"] = KlineInterval.ThirtyMinutes, ["1h"] = KlineInterval.OneHour,
            ["2h"] = KlineInterval.TwoHours, ["4h"] = KlineInterval.FourHours,
            ["6h"] = KlineInterval.SixHours, ["12h"] = KlineInterval.TwelveHours,
            ["1d"] = KlineInterval.OneDay, ["1w"] = KlineInterval.OneWeek,
            ["1M"] = KlineInterval.OneMonth,
        };

    /// <summary>Resolves an exchange name (case-insensitive) to one of the registered exchanges.</summary>
    public static bool TryParseExchange(string value, out ExchangeId id)
        => Exchanges.TryGetValue(value ?? string.Empty, out id);

    /// <summary>Resolves a kline interval string (e.g. "1m","4h","1d") to a <see cref="KlineInterval"/>.</summary>
    public static bool TryParseInterval(string value, out KlineInterval interval)
        => Intervals.TryGetValue(value ?? string.Empty, out interval);

    /// <summary>Parses "BASE/QUOTE" (e.g. "BTC/USDT") into a typed <see cref="Symbol"/>.</summary>
    /// <exception cref="FormatException">The input is not "BASE/QUOTE" with two known assets.</exception>
    public static Symbol ParseSymbol(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Split('/');
        if (parts.Length != 2 || !Asset.TryOf(parts[0], out var @base) || !Asset.TryOf(parts[1], out var quote))
            throw new FormatException($"Symbol must be 'BASE/QUOTE' (e.g. 'BTC/USDT'); got '{value}'.");
        return new Symbol(@base, quote);
    }
}
