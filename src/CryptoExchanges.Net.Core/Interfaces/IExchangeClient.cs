using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>
/// Unified entry point for interacting with a cryptocurrency exchange.
/// Provides access to market data, trading, and account services.
/// </summary>
public interface IExchangeClient : IAsyncDisposable
{
    /// <summary>The exchange identifier.</summary>
    ExchangeId ExchangeId { get; }

    /// <summary>Market data service for this exchange.</summary>
    IMarketDataService MarketData { get; }

    /// <summary>Trading service for this exchange.</summary>
    ITradingService Trading { get; }

    /// <summary>Account service for this exchange.</summary>
    IAccountService Account { get; }

    /// <summary>Pings the exchange REST API to verify connectivity.</summary>
    Task<bool> PingAsync(CancellationToken ct = default);
}
