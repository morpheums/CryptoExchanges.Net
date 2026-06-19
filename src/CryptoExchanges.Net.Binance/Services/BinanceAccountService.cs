using System.Text.Json.Serialization;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Binance.Services;

/// <summary>
/// Binance implementation of <see cref="IAccountService"/>.
/// </summary>
internal sealed class BinanceAccountService(IBinanceHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IAccountService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["omitZeroBalances"] = "true"
        };

        var response = await http.GetAsync<AccountDto>("/api/v3/account", parameters, true, ct).ConfigureAwait(false);

        return modelMapper.Map<BalanceDto, AssetBalance>(response.Balances);
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["omitZeroBalances"] = "true"
        };

        var response = await http.GetAsync<AccountDto>("/api/v3/account", parameters, true, ct).ConfigureAwait(false);

        var match = response.Balances
            .Select(modelMapper.Map<BalanceDto, AssetBalance>)
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
        BinanceRequestValidation.ValidateHistoryWindow(limit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = limit.ToString()
        };

        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString();
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString();

        var results = await http.GetAsync<List<FillDto>>("/api/v3/myTrades", parameters, true, ct).ConfigureAwait(false);

        // Trade.Symbol is taken from the caller's typed argument (the caller already holds it),
        // not resolved from the wire string, so a cold mapper cache can never make this throw.
        return results.Select(t => new Trade(
            symbol,
            t.Id.ToString(),
            BinanceValueParsers.ParseDecimal(t.Price),
            BinanceValueParsers.ParseDecimal(t.Qty),
            DateTimeOffset.FromUnixTimeMilliseconds(t.Time),
            t.IsBuyer,
            t.OrderId.ToString()
        )).ToList();
    }

}
