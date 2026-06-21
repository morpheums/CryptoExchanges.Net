using System.Net.Http.Json;
using System.Text;
using CryptoExchanges.Net.Kucoin.Resilience;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Kucoin;

/// <summary>
/// Internal HTTP wrapper for KuCoin V1/V2 REST. Builds requests and deserializes successful responses.
/// Signing (Unix-ms timestamp + base64 HMAC + signed passphrase), the KuCoin authentication headers
/// (<c>KC-API-KEY</c>/<c>KC-API-SIGN</c>/<c>KC-API-TIMESTAMP</c>/<c>KC-API-PASSPHRASE</c>/<c>KC-API-KEY-VERSION</c>),
/// retries, rate-limit handling, and typed error translation are all provided by the resilience
/// pipeline on the injected <see cref="HttpClient"/>, so any response that reaches this type is
/// already a success.
/// <para>
/// KuCoin signs the request path (<c>RequestUri.PathAndQuery</c>): the prehash is
/// <c>timestamp + METHOD + requestPath + body</c>. The configured <see cref="HttpClient.BaseAddress"/>
/// is the host root only (e.g. <c>https://api.kucoin.com</c>, no path) and this client builds the
/// full request path beginning with <c>/api/v1/...</c> plus the escaped query string. The resulting
/// <c>RequestUri.PathAndQuery</c> is therefore exactly the KuCoin <c>requestPath</c> that gets signed.
/// </para>
/// </summary>
internal sealed class KucoinHttpClient(HttpClient httpClient) : IKucoinHttpClient
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
        if (signed) KucoinSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        // KuCoin POST is JSON-bodied; the signing handler signs the exact raw body string it reads back.
        var json = JsonSerializer.Serialize(parameters ?? [], JsonOptions);
        return await PostJsonAsync<T>(endpoint, json, signed, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, object body, bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(body);
        var json = JsonSerializer.Serialize(body, JsonOptions);
        return await PostJsonAsync<T>(endpoint, json, signed, ct).ConfigureAwait(false);
    }

    private async Task<T> PostJsonAsync<T>(string endpoint, string json, bool signed, CancellationToken ct)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (signed) KucoinSigningRequest.MarkSigned(request);
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
        if (signed) KucoinSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    private static string BuildUrl(string endpoint, Dictionary<string, string>? parameters)
    {
        var query = ExchangeUrl.BuildQueryString(parameters);
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }
}
