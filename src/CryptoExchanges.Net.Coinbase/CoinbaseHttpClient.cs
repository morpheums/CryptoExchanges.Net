using System.Net.Http.Json;
using System.Text;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Coinbase;

/// <summary>
/// Internal HTTP wrapper for Coinbase Advanced Trade REST. JWT signing, retries, rate-limit handling,
/// and typed error translation are all provided by the resilience pipeline on the injected
/// <see cref="HttpClient"/>. BaseAddress is host-only (no path) so <c>RequestUri.PathAndQuery</c> equals
/// the path the JWT <c>uri</c> claim binds (sign-consistency with the Coinbase signer).
/// Callers pass the full path (e.g. <c>/api/v3/brokerage/products</c>) as the endpoint.
/// </summary>
internal sealed class CoinbaseHttpClient(HttpClient httpClient) : ICoinbaseHttpClient
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
        if (signed) CoinbaseSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
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
        if (signed) CoinbaseSigningRequest.MarkSigned(request);
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
        if (signed) CoinbaseSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    private static string BuildUrl(string endpoint, Dictionary<string, string>? parameters)
    {
        var query = ExchangeUrl.BuildQueryString(parameters);
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }
}
