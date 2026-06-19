using System.ComponentModel;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using ModelContextProtocol.Server;

namespace CryptoExchanges.Net.Mcp.Tools;

/// <summary>Read-only public market-data tools. No API credentials required.</summary>
[McpServerToolType]
public static class MarketDataTools
{
    private const string ExchangeParam = "Exchange id: one of binance, bybit, okx, bitget.";
    private const string SymbolParam = "Trading pair as BASE/QUOTE, e.g. 'BTC/USDT'.";

    [McpServerTool, Description("Latest price for a trading pair on an exchange.")]
    public static Task<ToolResult<decimal>> GetPrice(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam)] string symbol)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return Resolve(factory, exchange, symbol, (c, s, ct) => c.MarketData.GetPriceAsync(s, ct));
    }

    [McpServerTool, Description("24h ticker statistics for one pair, or all pairs if symbol is omitted.")]
    public static Task<ToolResult<IReadOnlyList<Ticker>>> GetTicker(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam + " Omit for all pairs.")] string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<IReadOnlyList<Ticker>>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() =>
        {
            Symbol? s = symbol is null ? null : ToolInputs.ParseSymbol(symbol);
            return client!.MarketData.GetTickersAsync(s, default);
        });
    }

    [McpServerTool, Description("Order book (bids/asks) for a pair, to the given depth.")]
    public static Task<ToolResult<OrderBook>> GetOrderBook(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam)] string symbol,
        [Description("Number of levels per side (default 100).")] int depth = 100)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return Resolve(factory, exchange, symbol, (c, s, ct) => c.MarketData.GetOrderBookAsync(s, depth, ct));
    }

    [McpServerTool, Description("Candlestick/kline data for a pair at an interval.")]
    public static Task<ToolResult<IReadOnlyList<Candlestick>>> GetKlines(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam)] string symbol,
        [Description("Interval (case-sensitive): 1m,3m,5m,15m,30m,1h,2h,4h,6h,8h,12h,1d,3d,1w,1M.")] string interval,
        [Description("Max candles (default 500).")] int limit = 500)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<IReadOnlyList<Candlestick>>.Failure(Unavailable(exchange)));
        if (!ToolInputs.TryParseInterval(interval, out var iv))
            return Task.FromResult(ToolResult<IReadOnlyList<Candlestick>>.Failure(
                new ToolError("BadInterval", $"Unsupported interval '{interval}'.")));
        return ToolRunner.RunAsync(() =>
        {
            var s = ToolInputs.ParseSymbol(symbol);
            return client!.MarketData.GetCandlesticksAsync(s, iv, null, null, limit, default);
        });
    }

    [McpServerTool, Description("Recent public trades for a pair.")]
    public static Task<ToolResult<IReadOnlyList<Trade>>> GetRecentTrades(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam)] string symbol,
        [Description("Max trades (default 500).")] int limit = 500)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return Resolve(factory, exchange, symbol, (c, s, ct) => c.MarketData.GetRecentTradesAsync(s, limit, ct));
    }

    [McpServerTool, Description("Exchange trading rules and the list of supported symbols.")]
    public static Task<ToolResult<ExchangeInfo>> GetExchangeInfo(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<ExchangeInfo>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() => client!.MarketData.GetExchangeInfoAsync(default));
    }

    // Shared path for the symbol-required tools.
    private static Task<ToolResult<T>> Resolve<T>(
        IExchangeClientFactory factory, string exchange, string symbol,
        Func<IExchangeClient, Symbol, CancellationToken, Task<T>> call)
    {
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<T>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() =>
        {
            var s = ToolInputs.ParseSymbol(symbol);
            return call(client!, s, default);
        });
    }

    private static ToolError Unavailable(string exchange) =>
        new("ExchangeUnavailable", $"Exchange '{exchange}' is not one of: binance, bybit, okx, bitget.");
}
