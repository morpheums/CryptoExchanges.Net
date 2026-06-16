using System.Text.Json.Serialization;

namespace CryptoExchanges.Net.Binance.Services;

// ---------------------------------------------------------------------------
//  Binance Account DTOs
// ---------------------------------------------------------------------------

internal sealed record BinanceAccountResponse
{
    [JsonPropertyName("balances")]
    public List<BinanceBalance> Balances { get; init; } = [];
}

internal sealed record BinanceBalance
{
    [JsonPropertyName("asset")]
    public string Asset { get; init; } = string.Empty;

    [JsonPropertyName("free")]
    public string Free { get; init; } = "0";

    [JsonPropertyName("locked")]
    public string Locked { get; init; } = "0";
}

internal sealed record BinanceTradeHistoryResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("qty")]
    public string Qty { get; init; } = "0";

    [JsonPropertyName("quoteQty")]
    public string QuoteQty { get; init; } = "0";

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("isBuyer")]
    public bool IsBuyer { get; init; }
}

// ---------------------------------------------------------------------------
//  BinanceAccountService
// ---------------------------------------------------------------------------

/// <summary>
/// Binance implementation of <see cref="IAccountService"/>.
/// </summary>
internal sealed class BinanceAccountService(BinanceHttpClient http) : IAccountService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["omitZeroBalances"] = "true",
            ["recvWindow"] = "5000"
        };

        var response = await http.GetAsync<BinanceAccountResponse>("/api/v3/account", parameters, true, ct).ConfigureAwait(false);

        return response.Balances
            .Select(b => new AssetBalance(b.Asset, ParseDecimal(b.Free), ParseDecimal(b.Locked)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AssetBalance> GetBalanceAsync(string asset, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["omitZeroBalances"] = "true",
            ["recvWindow"] = "5000"
        };

        var response = await http.GetAsync<BinanceAccountResponse>("/api/v3/account", parameters, true, ct).ConfigureAwait(false);

        var match = response.Balances.FirstOrDefault(b =>
            string.Equals(b.Asset, asset, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return new AssetBalance(asset, 0, 0);

        return new AssetBalance(match.Asset, ParseDecimal(match.Free), ParseDecimal(match.Locked));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetTradeHistoryAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToString(),
            ["limit"] = limit.ToString(),
            ["recvWindow"] = "5000"
        };

        var results = await http.GetAsync<List<BinanceTradeHistoryResponse>>("/api/v3/myTrades", parameters, true, ct).ConfigureAwait(false);

        return results.Select(t => new Trade(
            symbol,
            t.Id.ToString(),
            ParseDecimal(t.Price),
            ParseDecimal(t.Qty),
            DateTimeOffset.FromUnixTimeMilliseconds(t.Time),
            t.IsBuyer,
            t.OrderId.ToString()
        )).ToList();
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
