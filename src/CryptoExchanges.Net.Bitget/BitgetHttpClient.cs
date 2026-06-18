using System.Net.Http.Json;
using System.Text;
using CryptoExchanges.Net.Bitget.Resilience;

namespace CryptoExchanges.Net.Bitget;

/// <summary>
/// Internal HTTP wrapper for Bitget V2 REST. Builds requests and deserializes successful responses.
/// Signing (timestamp + base64 HMAC), the Bitget authentication headers
/// (<c>ACCESS-KEY</c>/<c>ACCESS-SIGN</c>/<c>ACCESS-TIMESTAMP</c>/<c>ACCESS-PASSPHRASE</c>),
/// retries, rate-limit handling, and typed error translation are all provided by the resilience
/// pipeline on the injected <see cref="HttpClient"/>, so any response that reaches this type is
/// already a success.
/// <para>
/// Bitget's prehash is <c>timestamp + METHOD + requestPath + ('?'+queryString when present) + body</c>,
/// and <c>BitgetSigningHandler</c> reassembles it from <c>RequestUri.AbsolutePath</c> and
/// <c>RequestUri.Query</c> SEPARATELY. To keep the signed string byte-consistent, the configured
/// <see cref="HttpClient.BaseAddress"/> is the host root only (e.g. <c>https://api.bitget.com</c>, no
/// path) and this client builds the full request path beginning with <c>/api/v2/...</c> plus the
/// escaped query string. The resulting <c>RequestUri.AbsolutePath</c> is therefore exactly the Bitget
/// <c>requestPath</c> and <c>RequestUri.Query</c> is exactly the signed query. Callers pass the full
/// path (e.g. <c>/api/v2/spot/market/tickers</c>) as the endpoint.
/// </para>
/// <para>
/// GET/DELETE append an escaped query string to the path; POST sends a JSON body. The signing handler
/// reads the serialized JSON back to compute the signature, so the serialized JSON must be the wire
/// body verbatim.
/// </para>
/// </summary>
internal sealed class BitgetHttpClient(HttpClient httpClient) : IBitgetHttpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(endpoint, parameters));
        if (signed) BitgetSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        // Bitget V2 POST is JSON-bodied; the signing handler signs the exact raw body string it reads
        // back, so the serialized JSON must be the wire body verbatim.
        var json = JsonSerializer.Serialize(parameters ?? [], JsonOptions);
        return await PostJsonAsync<T>(endpoint, json, signed, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, object body, bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(body);
        // Object-body overload for endpoints whose wire body is a JSON array or nested object (e.g.
        // batch order placement). The serialized JSON is the verbatim body the signer reads.
        var json = JsonSerializer.Serialize(body, JsonOptions);
        return await PostJsonAsync<T>(endpoint, json, signed, ct).ConfigureAwait(false);
    }

    private async Task<T> PostJsonAsync<T>(string endpoint, string json, bool signed, CancellationToken ct)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (signed) BitgetSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> DeleteAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUrl(endpoint, parameters));
        if (signed) BitgetSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    private static string BuildUrl(string endpoint, Dictionary<string, string>? parameters)
    {
        var query = BuildQueryString(parameters);
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }

    private static string BuildQueryString(Dictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kvp.Key)).Append('=').Append(Uri.EscapeDataString(kvp.Value));
        }
        return sb.ToString();
    }
}
