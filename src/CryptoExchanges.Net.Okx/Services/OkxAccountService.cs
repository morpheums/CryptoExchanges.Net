using CryptoExchanges.Net.Okx.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Okx.Services;

/// <summary>
/// OKX implementation of <see cref="IAccountService"/> against the V5 unified-account REST API.
/// </summary>
internal sealed class OkxAccountService(IOkxHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IAccountService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        var details = await FetchBalanceDetailsAsync(null, ct).ConfigureAwait(false);

        // OKX returns the full currency list including zero balances; trim to non-zero to match the
        // venue-neutral "non-zero balances" contract other exchanges honour server-side.
        return details
            .Select(modelMapper.Map<OkxBalanceDetail, AssetBalance>)
            .Where(b => b.Total != 0m)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default)
    {
        var details = await FetchBalanceDetailsAsync(asset.Ticker, ct).ConfigureAwait(false);

        var match = details
            .Select(modelMapper.Map<OkxBalanceDetail, AssetBalance>)
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
        // The IExchangeClient default (500) exceeds OKX V5's per-call max (100); clamp rather than
        // throw so the common default-parameter call path succeeds. A value < 1 still fails validation.
        var effectiveLimit = Math.Min(limit, OkxRequestValidation.MaxHistoryLimit);
        OkxRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["instType"] = OkxRequestValidation.SpotInstType,
            ["instId"] = mapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["begin"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["end"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.GetAsync<OkxResponse<OkxFill>>("/api/v5/trade/fills", parameters, true, ct).ConfigureAwait(false);

        // Trade.Symbol is taken from the caller's typed argument (the caller already holds it), not
        // resolved from the wire string, so a cold mapper cache can never make this throw.
        return response.Data.Select(f => new Trade(
            symbol,
            f.TradeId,
            OkxValueParsers.ParseDecimal(f.FillPx),
            OkxValueParsers.ParseDecimal(f.FillSz),
            DateTimeOffset.FromUnixTimeMilliseconds(OkxValueParsers.ParseMs(f.Ts)),
            // IsBuyerMaker: a buy fill that is the maker, or a sell fill that is the taker.
            f.Side == "buy" ? f.ExecType == "M" : f.ExecType != "M",
            f.OrdId
        )).ToList();
    }

    private async Task<IReadOnlyList<OkxBalanceDetail>> FetchBalanceDetailsAsync(string? ccy, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(ccy))
            parameters["ccy"] = ccy;

        var response = await http.GetAsync<OkxResponse<OkxBalanceAccount>>("/api/v5/account/balance", parameters, true, ct).ConfigureAwait(false);
        return response.Data.SelectMany(a => a.Details).ToList();
    }

}
