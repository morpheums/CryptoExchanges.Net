using System.Text;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Kraken.Resilience;
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

    private static readonly KrakenErrorTranslator ErrorTranslator = new();

    // Wsname→legacyCode reverse table (e.g. "XBT/USD"→"XXBTZUSD"); warmed by GetExchangeInfoAsync.
    private volatile Dictionary<string, string> _wsnameToLegacy = [];

    /// <summary>
    /// Updates the reverse wsname→legacyCode table from a freshly fetched AssetPairs response.
    /// Using the legacy pair code in REST params avoids URL-encoding issues with the wsname slash.
    /// </summary>
    internal void UpdateLegacyTable(Dictionary<string, string> legacyToWsname)
    {
        ArgumentNullException.ThrowIfNull(legacyToWsname);
        var reverse = new Dictionary<string, string>(legacyToWsname.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in legacyToWsname)
            reverse[kvp.Value] = kvp.Key;
        _wsnameToLegacy = reverse;
    }

    /// <summary>
    /// Resolves <paramref name="symbol"/> to its Kraken REST pair string.
    /// Prefers the legacy pair code (e.g. XXBTZUSD) when the warm table is populated;
    /// falls back to <see cref="KrakenSymbolFormat"/> wsname when cold.
    /// </summary>
    public string ToWire(Symbol symbol)
    {
        var wsname = symbolMapper.ToWire(symbol);
        return _wsnameToLegacy.TryGetValue(wsname, out var legacy) ? legacy : wsname;
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
        return await ReadResultAsync<T>(response, ct).ConfigureAwait(false);
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
        return await ReadResultAsync<T>(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a Kraken 200 response, surfacing in-body errors. Kraken signals failures via a non-empty
    /// <c>error[]</c> array on an HTTP 200, which the shared (non-2xx) error pipeline never sees — so this
    /// inspects the body and throws the translated typed exception before deserializing the payload.
    /// </summary>
    private static async Task<T> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (KrakenErrorTranslator.HasError(body))
            throw ErrorTranslator.Translate(response, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    private static string BuildUrl(string endpoint, Dictionary<string, string>? parameters)
    {
        var query = ExchangeUrl.BuildQueryString(parameters);
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }
}
