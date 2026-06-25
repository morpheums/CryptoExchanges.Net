using System.Globalization;
using CryptoExchanges.Net.Kraken.Dtos;
using CryptoExchanges.Net.Kraken.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kraken.Services;

/// <summary>Kraken implementation of <see cref="IAccountService"/> against the private REST API.</summary>
internal sealed class KrakenAccountService(IKrakenHttpClient http, ISymbolMapper symbolMapper, IMapper modelMapper) : IAccountService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        var response = await http.PostAsync<ResponseDto<Dictionary<string, string>>>(
            "/0/private/Balance", signed: true, ct: ct).ConfigureAwait(false);

        var result = response.Result ?? [];
        return FlattenBalances(result)
            .Select(modelMapper.Map<BalanceDto, AssetBalance>)
            .Where(b => b.Total != 0m)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(Asset asset, CancellationToken ct = default)
    {
        var response = await http.PostAsync<ResponseDto<Dictionary<string, string>>>(
            "/0/private/Balance", signed: true, ct: ct).ConfigureAwait(false);

        var result = response.Result ?? [];
        var match = FlattenBalances(result)
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
        KrakenRequestValidation.ValidateHistoryWindow(limit, startTime, endTime);

        var parameters = new Dictionary<string, string>();

        if (startTime.HasValue)
            parameters["start"] = startTime.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["end"] = endTime.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var response = await http.PostAsync<ResponseDto<TradesHistoryResultDto>>(
            "/0/private/TradesHistory", parameters, signed: true, ct: ct).ConfigureAwait(false);

        IEnumerable<FillDto> fills = response.Result?.Trades is { } t ? t.Values : [];
        var wireSymbol = symbolMapper.ToWire(symbol);

        return fills
            .Where(f => string.Equals(f.Pair, wireSymbol, StringComparison.OrdinalIgnoreCase))
            .Select(f => modelMapper.Map<FillDto, Trade>(f))
            .Take(limit)
            .ToList();
    }

    private static IEnumerable<BalanceDto> FlattenBalances(Dictionary<string, string> raw)
    {
        foreach (var (asset, balance) in raw)
            yield return new BalanceDto { Asset = asset, Balance = balance };
    }
}
