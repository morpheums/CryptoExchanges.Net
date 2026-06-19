using CryptoExchanges.Net.Bitget.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Bitget.Services;

/// <summary>
/// Bitget implementation of <see cref="IAccountService"/> against the V2 spot REST API.
/// </summary>
internal sealed class BitgetAccountService(IBitgetHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IAccountService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        var balances = await FetchBalancesAsync(null, ct).ConfigureAwait(false);

        // Bitget returns the full coin list including zero balances; trim to non-zero to match the
        // venue-neutral "non-zero balances" contract other exchanges honour server-side.
        return balances
            .Select(modelMapper.Map<BalanceDto, AssetBalance>)
            .Where(b => b.Total != 0m)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default)
    {
        var balances = await FetchBalancesAsync(asset.Ticker, ct).ConfigureAwait(false);

        var match = balances
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
        // The IExchangeClient default (500) exceeds Bitget V2's per-call max (100); clamp rather than
        // throw so the common default-parameter call path succeeds. A value < 1 still fails validation.
        var effectiveLimit = Math.Min(limit, BitgetRequestValidation.MaxHistoryLimit);
        BitgetRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.GetAsync<ResponseDto<FillDto>>("/api/v2/spot/trade/fills", parameters, true, ct).ConfigureAwait(false);

        // Trade.Symbol is taken from the caller's typed argument (the caller already holds it), not
        // resolved from the wire string, so a cold mapper cache can never make this throw.
        return response.Data.Select(f => new Trade(
            symbol,
            f.TradeId,
            BitgetValueParsers.ParseDecimal(f.PriceAvg),
            BitgetValueParsers.ParseDecimal(f.Size),
            DateTimeOffset.FromUnixTimeMilliseconds(BitgetValueParsers.ParseMs(f.CTime)),
            // IsBuyerMaker: a buy fill that is the maker, or a sell fill that is the taker.
            f.Side == "buy" ? f.TradeScope == "maker" : f.TradeScope != "maker",
            f.OrderId
        )).ToList();
    }

    private async Task<IReadOnlyList<BalanceDto>> FetchBalancesAsync(string? coin, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(coin))
            parameters["coin"] = coin;

        var response = await http.GetAsync<ResponseDto<BalanceDto>>("/api/v2/spot/account/assets", parameters, true, ct).ConfigureAwait(false);
        return response.Data;
    }
}
