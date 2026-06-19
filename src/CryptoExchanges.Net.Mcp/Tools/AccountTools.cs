using System.ComponentModel;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using ModelContextProtocol.Server;

namespace CryptoExchanges.Net.Mcp.Tools;

/// <summary>Read-only account tools. Require the user's read-scoped API credentials for the exchange.</summary>
[McpServerToolType]
public static class AccountTools
{
    private const string ExchangeParam = "Exchange id: one of binance, bybit, okx, bitget.";
    private const string SymbolParam = "Trading pair as BASE/QUOTE, e.g. 'BTC/USDT'.";

    [McpServerTool, Description("All non-zero asset balances for the account. Requires API credentials.")]
    public static Task<ToolResult<IReadOnlyList<AssetBalance>>> GetBalances(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        return Run(factory, exchange, (c, ct) => c.Account.GetBalancesAsync(ct));
    }

    [McpServerTool, Description("Balance of a single asset (e.g. 'BTC'). Requires API credentials.")]
    public static Task<ToolResult<AssetBalance>> GetBalance(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description("Asset ticker, e.g. 'BTC'.")] string asset)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        // null/empty/whitespace/unknown asset → structured BadRequest error, not ArgumentException.
        // MCP-boundary design: domain-invalid inputs return ToolError, not throw (architect-endorsed).
        if (!Asset.TryOf(asset, out var a))
            return Task.FromResult(ToolResult<AssetBalance>.Failure(
                new ToolError("BadRequest", $"Unknown or empty asset '{asset}'.")));
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<AssetBalance>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() => client!.Account.GetBalanceAsync(a, default));
    }

    [McpServerTool, Description("Open orders, optionally filtered by pair. Requires API credentials.")]
    public static Task<ToolResult<IReadOnlyList<Order>>> GetOpenOrders(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam + " Omit for all pairs.")] string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        return Run(factory, exchange, (c, ct) =>
        {
            Symbol? s = symbol is null ? null : ToolInputs.ParseSymbol(symbol);
            return c.Trading.GetOpenOrdersAsync(s, ct);
        });
    }

    [McpServerTool, Description("A specific order by its exchange order id. Requires API credentials.")]
    public static Task<ToolResult<Order>> GetOrder(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam)] string symbol,
        [Description("Exchange-assigned order id.")] string orderId)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        return Resolve(factory, exchange, symbol, (c, s, ct) => c.Trading.GetOrderAsync(s, orderId, ct));
    }

    [McpServerTool, Description("Historical orders for a pair. Requires API credentials.")]
    public static Task<ToolResult<IReadOnlyList<Order>>> GetOrderHistory(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam)] string symbol,
        [Description("Max orders (default 500).")] int limit = 500)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit < 1)
            return Task.FromResult(ToolResult<IReadOnlyList<Order>>.Failure(BadCount("limit")));
        return Resolve(factory, exchange, symbol, (c, s, ct) => c.Trading.GetOrderHistoryAsync(s, limit, null, null, ct));
    }

    [McpServerTool, Description("The account's own executed trades for a pair. Requires API credentials.")]
    public static Task<ToolResult<IReadOnlyList<Trade>>> GetTradeHistory(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam)] string symbol,
        [Description("Max trades (default 500).")] int limit = 500)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limit < 1)
            return Task.FromResult(ToolResult<IReadOnlyList<Trade>>.Failure(BadCount("limit")));
        return Resolve(factory, exchange, symbol, (c, s, ct) => c.Account.GetTradeHistoryAsync(s, limit, null, null, ct));
    }

    // Shared path for tools that only need exchange resolution (no symbol parsing).
    private static Task<ToolResult<T>> Run<T>(
        IExchangeClientFactory factory, string exchange,
        Func<IExchangeClient, CancellationToken, Task<T>> call)
    {
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<T>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() => call(client!, default));
    }

    // Shared path for tools that require symbol parsing.
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

    private static ToolError BadCount(string name) =>
        new("BadRequest", $"{name} must be >= 1.");
}
