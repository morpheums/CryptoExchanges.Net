using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

// ──────────────────────────────────────────────
//  CryptoExchanges.Net — Basic Usage Demo
// ──────────────────────────────────────────────

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  CryptoExchanges.Net — Basic Usage Demo ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();

// Create client from environment variables (or empty keys for public-only access)
var client = BinanceExchangeClient.CreateFromEnvironment();
Console.WriteLine($"Exchange: {client.ExchangeId}");
Console.WriteLine();

// ── 1. Ping ──
Console.WriteLine("─── Ping ───");
try
{
    var isAlive = await client.PingAsync();
    Console.WriteLine($"  Binance API reachable: {isAlive}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Ping failed: {ex.Message}");
}
Console.WriteLine();

// ── 2. Get Price ──
Console.WriteLine("─── Latest Price (BTCUSDT) ───");
try
{
    var btcSymbol = Symbol.Parse("BTCUSDT");
    var price = await client.MarketData.GetPriceAsync(btcSymbol);
    Console.WriteLine($"  BTC/USDT = ${price:N2}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Price lookup failed: {ex.Message}");
}
Console.WriteLine();

// ── 3. 24h Ticker ──
Console.WriteLine("─── 24h Ticker (ETHUSDT) ───");
try
{
    var ethSymbol = Symbol.Parse("ETHUSDT");
    var tickers = await client.MarketData.GetTickersAsync(ethSymbol);
    foreach (var ticker in tickers)
    {
        Console.WriteLine($"  {ticker.Symbol}: Last=${ticker.LastPrice:N2}, " +
                          $"24h Change={ticker.PriceChangePercent:F2}%, " +
                          $"High=${ticker.HighPrice:N2}, Low=${ticker.LowPrice:N2}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Ticker lookup failed: {ex.Message}");
}
Console.WriteLine();

// ── 4. Order Book (top 5 levels) ──
Console.WriteLine("─── Order Book (BTCUSDT, depth=5) ───");
try
{
    var btcSymbol = Symbol.Parse("BTCUSDT");
    var book = await client.MarketData.GetOrderBookAsync(btcSymbol, 5);
    Console.WriteLine($"  Last Update ID: {book.LastUpdateId}");
    Console.WriteLine("  Asks (sell orders):");
    foreach (var ask in book.Asks.Take(5))
        Console.WriteLine($"    {ask.Price:N2} → Qty: {ask.Quantity:F6}");
    Console.WriteLine("  Bids (buy orders):");
    foreach (var bid in book.Bids.Take(5))
        Console.WriteLine($"    {bid.Price:N2} → Qty: {bid.Quantity:F6}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Order book lookup failed: {ex.Message}");
}
Console.WriteLine();

// ── 5. Candlesticks ──
Console.WriteLine("─── Recent Candles (BTCUSDT, 1h, last 5) ───");
try
{
    var btcSymbol = Symbol.Parse("BTCUSDT");
    var candles = await client.MarketData.GetCandlesticksAsync(
        btcSymbol, KlineInterval.OneHour, limit: 5);

    foreach (var candle in candles)
    {
        Console.WriteLine($"  [{candle.OpenTime:HH:mm}] O={candle.Open:N2} H={candle.High:N2} " +
                          $"L={candle.Low:N2} C={candle.Close:N2} V={candle.Volume:F4}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Candlestick lookup failed: {ex.Message}");
}
Console.WriteLine();

// ── 6. Recent Trades ──
Console.WriteLine("─── Recent Trades (BTCUSDT, last 3) ───");
try
{
    var btcSymbol = Symbol.Parse("BTCUSDT");
    var trades = await client.MarketData.GetRecentTradesAsync(btcSymbol, 3);
    foreach (var trade in trades)
    {
        var side = trade.IsBuyerMaker ? "SELL" : "BUY";
        Console.WriteLine($"  [{trade.Timestamp:HH:mm:ss}] {side} {trade.Quantity:F6} @ {trade.Price:N2}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Trade lookup failed: {ex.Message}");
}
Console.WriteLine();

// ── 7. Account Balances (if API keys configured) ──
Console.WriteLine("─── Account Balances ───");
try
{
    var balances = await client.Account.GetBalancesAsync();
    if (balances.Count == 0)
    {
        Console.WriteLine("  No balances returned (API keys may not be configured).");
    }
    else
    {
        foreach (var balance in balances)
        {
            Console.WriteLine($"  {balance.Asset}: Free={balance.Free:F8}, Locked={balance.Locked:F8}, Total={balance.Total:F8}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Balance lookup failed: {ex.Message} (this is expected without valid API keys)");
}
Console.WriteLine();

// ── 8. Exchange Info ──
Console.WriteLine("─── Exchange Info (sample) ───");
try
{
    var info = await client.MarketData.GetExchangeInfoAsync();
    Console.WriteLine($"  Exchange: {info.ExchangeName}");
    Console.WriteLine($"  Symbols: {info.Symbols.Count}");
    Console.WriteLine($"  Rate Limits: {info.RateLimits.Count}");

    // Show first 3 symbols
    foreach (var symInfo in info.Symbols.Take(3))
    {
        var types = string.Join(", ", symInfo.AllowedOrderTypes);
        Console.WriteLine($"    {symInfo.Symbol}: [{types}]");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Exchange info failed: {ex.Message}");
}
Console.WriteLine();

Console.WriteLine("Demo complete.");
