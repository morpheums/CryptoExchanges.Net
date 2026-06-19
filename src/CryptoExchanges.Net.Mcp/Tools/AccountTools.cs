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
        // asset is intentionally NOT guarded here — an empty/whitespace asset is a valid
        // user error that we surface as a structured ToolError rather than an ArgumentException.
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<AssetBalance>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() =>
        {
            if (!Asset.TryOf(asset, out var a))
                throw new FormatException($"Unknown asset '{asset}'.");
            return client!.Account.GetBalanceAsync(a, default);
        });
    }

    [McpServerTool, Description("Open orders, optionally filtered by pair. Requires API credentials.")]
    public static Task<ToolResult<IReadOnlyList<Order>>> GetOpenOrders(
        IExchangeClientFactory factory,
        [Description(ExchangeParam)] string exchange,
        [Description(SymbolParam + " Omit for all pairs.")] string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<IReadOnlyList<Order>>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() =>
        {
            Symbol? s = symbol is null ? null : ToolInputs.ParseSymbol(symbol);
            return client!.Trading.GetOpenOrdersAsync(s, default);
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
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<Order>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() =>
        {
            var s = ToolInputs.ParseSymbol(symbol);
            return client!.Trading.GetOrderAsync(s, orderId, default);
        });
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
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<IReadOnlyList<Order>>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() =>
        {
            var s = ToolInputs.ParseSymbol(symbol);
            return client!.Trading.GetOrderHistoryAsync(s, limit, null, null, default);
        });
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
        if (!ToolInputs.TryParseExchange(exchange, out var id) || !factory.TryGet(id, out var client))
            return Task.FromResult(ToolResult<IReadOnlyList<Trade>>.Failure(Unavailable(exchange)));
        return ToolRunner.RunAsync(() =>
        {
            var s = ToolInputs.ParseSymbol(symbol);
            return client!.Account.GetTradeHistoryAsync(s, limit, null, null, default);
        });
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

    private static ToolError Unavailable(string exchange) =>
        new("ExchangeUnavailable", $"Exchange '{exchange}' is not one of: binance, bybit, okx, bitget.");
}
