using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>
/// Bybit implementation of <see cref="IAccountService"/> against the V5 unified-account REST API.
/// </summary>
internal sealed class BybitAccountService(IBybitHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IAccountService
{
    private const string SpotCategory = "spot";
    private const string UnifiedAccount = "UNIFIED";

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        var coins = await FetchCoinBalancesAsync(ct).ConfigureAwait(false);

        // Bybit returns the full coin list including zero balances; trim to non-zero to match the
        // venue-neutral "non-zero balances" contract other exchanges honour server-side.
        return coins
            .Select(modelMapper.Map<BybitCoinBalance, AssetBalance>)
            .Where(b => b.Total != 0m)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["accountType"] = UnifiedAccount,
            ["coin"] = asset.Ticker
        };

        var response = await http.GetAsync<BybitResponse<BybitListResult<BybitWalletAccount>>>("/v5/account/wallet-balance", parameters, true, ct).ConfigureAwait(false);
        var coins = response.Result?.List.SelectMany(a => a.Coin) ?? [];

        var match = coins
            .Select(modelMapper.Map<BybitCoinBalance, AssetBalance>)
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
        // The IExchangeClient default (500) exceeds Bybit V5's per-call max (50); clamp rather than
        // throw so the common default-parameter call path succeeds. A value < 1 still fails validation.
        var effectiveLimit = Math.Min(limit, BybitRequestValidation.MaxHistoryLimit);
        BybitRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString()
        };

        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString();
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString();

        var response = await http.GetAsync<BybitResponse<BybitListResult<BybitExecution>>>("/v5/execution/list", parameters, true, ct).ConfigureAwait(false);
        var executions = response.Result?.List ?? [];

        // Trade.Symbol is taken from the caller's typed argument (the caller already holds it),
        // not resolved from the wire string, so a cold mapper cache can never make this throw.
        return executions.Select(e => new Trade(
            symbol,
            e.ExecId,
            BybitValueParsers.ParseDecimal(e.ExecPrice),
            BybitValueParsers.ParseDecimal(e.ExecQty),
            DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(e.ExecTime, System.Globalization.CultureInfo.InvariantCulture)),
            // IsBuyerMaker: a buy fill that is the maker, or a sell fill that is the taker.
            e.Side == "Buy" ? e.IsMaker : !e.IsMaker,
            e.OrderId
        )).ToList();
    }

    private async Task<IReadOnlyList<BybitCoinBalance>> FetchCoinBalancesAsync(CancellationToken ct)
    {
        var parameters = new Dictionary<string, string> { ["accountType"] = UnifiedAccount };
        var response = await http.GetAsync<BybitResponse<BybitListResult<BybitWalletAccount>>>("/v5/account/wallet-balance", parameters, true, ct).ConfigureAwait(false);
        return response.Result?.List.SelectMany(a => a.Coin).ToList() ?? [];
    }
}
