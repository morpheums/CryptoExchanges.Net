using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kucoin.Services;

/// <summary>
/// KuCoin implementation of <see cref="IAccountService"/> against the V1 spot REST API.
/// </summary>
internal sealed class KucoinAccountService(IKucoinHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IAccountService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        // KuCoin /api/v1/accounts returns a flat list of per-currency balance records.
        // type=trade filters to the spot trading account (excludes main/margin/futures).
        var parameters = new Dictionary<string, string> { ["type"] = "trade" };
        var response = await http.GetAsync<ResponseDto<List<BalanceDto>>>("/api/v1/accounts", parameters, true, ct).ConfigureAwait(false);
        var items = response.Data ?? [];

        // KuCoin returns all currencies including zero balances; trim to non-zero.
        return items
            .Select(modelMapper.Map<BalanceDto, AssetBalance>)
            .Where(b => b.Total != 0m)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["type"] = "trade",
            ["currency"] = asset.Ticker
        };
        var response = await http.GetAsync<ResponseDto<List<BalanceDto>>>("/api/v1/accounts", parameters, true, ct).ConfigureAwait(false);
        var items = response.Data ?? [];

        var match = items
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
        // KuCoin fills endpoint: /api/v1/fills uses a paginated ListDto wrapper.
        // KuCoin caps pageSize at 500.
        var effectiveLimit = Math.Min(limit, KucoinRequestValidation.MaxHistoryLimit);
        KucoinRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["pageSize"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["startAt"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["endAt"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.GetAsync<ResponseDto<ListDto<FillDto>>>("/api/v1/fills", parameters, true, ct).ConfigureAwait(false);
        var items = response.Data?.Items ?? [];

        // FillDto -> Trade via DeltaMapper profile (symbol resolved from wire string in the dto).
        return modelMapper.Map<FillDto, Trade>(items);
    }
}
