using System.Net.Http.Json;
using System.Text;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Kraken;

/// <summary>
/// Internal HTTP wrapper for the Kraken REST API. Signing, retries, rate-limit handling,
/// and typed error translation are provided by the resilience pipeline; any response reaching
/// this type is already a success. Public GETs append a query string; private POSTs are
/// <c>application/x-www-form-urlencoded</c> as the signing handler expects. BaseAddress is
/// host-only (no path). <c>ToWire</c> checks the warm legacy table before the format fallback
/// (ADR-010-006).
/// </summary>
internal sealed class KrakenHttpClient(HttpClient httpClient, ISymbolMapper symbolMapper) : IKrakenHttpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Legacy→wsname table warmed by GetExchangeInfoAsync. Replaced atomically; reads are safe
    // after population because no concurrent writes occur once the field is set.
    private volatile Dictionary<string, string> _legacyToWsname = [];

    /// <summary>Updates the warm legacy→wsname lookup table from a freshly fetched AssetPairs response.</summary>
    internal void UpdateLegacyTable(Dictionary<string, string> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _legacyToWsname = table;
    }

    /// <summary>
    /// Resolves <paramref name="symbol"/> to its Kraken wire string (wsname, e.g. XBT/USD).
    /// Checks the warm legacy table first; falls back to <see cref="KrakenSymbolFormat"/> when cold.
    /// </summary>
    public string ToWire(Symbol symbol)
    {
        var wire = symbolMapper.ToWire(symbol);
        return _legacyToWsname.TryGetValue(wire, out var wsname) ? wsname : wire;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(endpoint, parameters));
        if (signed) KrakenSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        // Kraken private endpoints use form-encoded bodies; the signing handler reads this raw body.
        var form = ExchangeUrl.BuildQueryString(parameters);
        using var content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (signed) KrakenSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    private static string BuildUrl(string endpoint, Dictionary<string, string>? parameters)
    {
        var query = ExchangeUrl.BuildQueryString(parameters);
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }
}
