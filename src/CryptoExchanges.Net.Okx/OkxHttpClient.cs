using System.Net.Http.Json;
using System.Text;
using CryptoExchanges.Net.Okx.Resilience;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Okx;

/// <summary>
/// Internal HTTP wrapper for OKX V5 REST. Builds requests and deserializes successful responses.
/// Signing (timestamp + base64 HMAC), the OKX authentication headers
/// (<c>OK-ACCESS-KEY</c>/<c>OK-ACCESS-SIGN</c>/<c>OK-ACCESS-TIMESTAMP</c>/<c>OK-ACCESS-PASSPHRASE</c>),
/// retries, rate-limit handling, and typed error translation are all provided by the resilience
/// pipeline on the injected <see cref="HttpClient"/>, so any response that reaches this type is
/// already a success.
/// <para>
/// OKX signs the request path (<c>RequestUri.PathAndQuery</c>): the prehash is
/// <c>timestamp + METHOD + requestPath + body</c>. To keep the signed string byte-consistent with
/// what the <c>OkxSigningHandler</c> reassembles, the configured <see cref="HttpClient.BaseAddress"/>
/// is the host root only (e.g. <c>https://www.okx.com</c>, no path) and this client builds the
/// full request path beginning with <c>/api/v5/...</c> plus the escaped query string. The resulting
/// <c>RequestUri.PathAndQuery</c> is therefore exactly the OKX <c>requestPath</c> that gets signed.
/// Callers pass the full path (e.g. <c>/api/v5/market/tickers</c>) as the endpoint.
/// </para>
/// <para>
/// GET/DELETE append an escaped query string to the path; POST sends a JSON body. The signing
/// handler reads the serialized JSON back to compute the signature, so the serialized JSON must be
/// the wire body verbatim.
/// </para>
/// </summary>
internal sealed class OkxHttpClient(HttpClient httpClient) : IOkxHttpClient
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
        if (signed) OkxSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        // OKX V5 POST is JSON-bodied; the signing handler signs the exact raw body string it reads
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
        // /api/v5/trade/cancel-batch-orders). The serialized JSON is the verbatim body the signer reads.
        var json = JsonSerializer.Serialize(body, JsonOptions);
        return await PostJsonAsync<T>(endpoint, json, signed, ct).ConfigureAwait(false);
    }

    private async Task<T> PostJsonAsync<T>(string endpoint, string json, bool signed, CancellationToken ct)
    {
        // OKX V5 POST is JSON-bodied; the signing handler signs the exact raw body string it reads
        // back, so the serialized JSON must be the wire body verbatim.
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (signed) OkxSigningRequest.MarkSigned(request);
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
        if (signed) OkxSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    private static string BuildUrl(string endpoint, Dictionary<string, string>? parameters)
    {
        var query = ExchangeUrl.BuildQueryString(parameters);
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }
}
