using System.Net.Http.Json;
using System.Text;
using CryptoExchanges.Net.Kucoin.Resilience;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Kucoin;

/// <summary>
/// Internal HTTP wrapper for KuCoin V1/V2 REST. Builds requests and deserializes responses;
/// signing, retries, rate-limiting, and error translation are owned by the resilience pipeline
/// on the injected <see cref="HttpClient"/>, so every response that reaches this type is already a success.
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
