using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>Provides access to account information and trade history.</summary>
public interface IAccountService
{
    /// <summary>Retrieves all non-zero asset balances.</summary>
    Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default);

    /// <summary>Retrieves the balance for a specific asset.</summary>
    Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default);

    /// <summary>Retrieves trade history for a specific symbol.</summary>
    /// <param name="symbol">The trading pair symbol.</param>
    /// <param name="limit">Maximum number of trades to retrieve.</param>
    /// <param name="startTime">Optional start time filter.</param>
    /// <param name="endTime">Optional end time filter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Trade>> GetTradeHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default);
}
