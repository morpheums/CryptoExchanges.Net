using CryptoExchanges.Net.Coinbase.Dtos;
using CryptoExchanges.Net.Coinbase.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Coinbase.Services;

/// <summary>
/// Coinbase Advanced Trade implementation of <see cref="IAccountService"/> against the V3 brokerage REST API.
/// </summary>
internal sealed class CoinbaseAccountService(ICoinbaseHttpClient http, ISymbolMapper symbolMapper, IMapper modelMapper) : IAccountService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        var accounts = await FetchAccountsAsync(ct).ConfigureAwait(false);
        return accounts
            .Select(modelMapper.Map<AccountDto, AssetBalance>)
            .Where(b => b.Total != 0m)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default)
    {
        var accounts = await FetchAccountsAsync(ct).ConfigureAwait(false);

        var match = accounts
            .Select(modelMapper.Map<AccountDto, AssetBalance>)
            .FirstOrDefault(b => b.Asset == asset);

        return match.Asset == asset ? match : new AssetBalance(asset, 0, 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetTradeHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        // Coinbase fills endpoint caps at 100/call; clamp so the IExchangeClient default (500) succeeds.
        var effectiveLimit = Math.Min(limit, CoinbaseRequestValidation.MaxFillsLimit);

        var parameters = new Dictionary<string, string>
        {
            ["product_id"] = symbolMapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["start_sequence_timestamp"] = startTime.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["end_sequence_timestamp"] = endTime.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        var fills = await http.GetPropertyAsync<List<FillDto>>("/api/v3/brokerage/orders/historical/fills", "fills", parameters, true, ct).ConfigureAwait(false) ?? [];

        return fills.Select(f => new Trade(
            symbol,
            f.TradeId,
            CoinbaseValueParsers.ParseDecimal(f.Price),
            CoinbaseValueParsers.ParseDecimal(f.Size),
            CoinbaseValueParsers.ParseRfc3339ToTimestamp(f.TradeTime) ?? DateTimeOffset.MinValue,
            // SELL taker means the buyer was the maker.
            f.Side == "SELL",
            f.OrderId
        )).ToList();
    }

    private async Task<IReadOnlyList<AccountDto>> FetchAccountsAsync(CancellationToken ct) =>
        await http.GetPropertyAsync<List<AccountDto>>("/api/v3/brokerage/accounts", "accounts", signed: true, ct: ct).ConfigureAwait(false) ?? [];
}
