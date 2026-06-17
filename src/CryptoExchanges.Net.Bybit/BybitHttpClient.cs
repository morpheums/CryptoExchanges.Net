using System.Net.Http.Json;
using System.Text;
using CryptoExchanges.Net.Bybit.Resilience;

namespace CryptoExchanges.Net.Bybit;

/// <summary>
/// Internal HTTP wrapper for Bybit V5 REST. Builds requests and deserializes successful
/// responses. Signing (timestamp + HMAC), the API-key header, the recv-window header, retries,
/// rate-limit handling, and typed error translation are all provided by the resilience pipeline
/// on the injected <see cref="HttpClient"/>, so any response that reaches this type is already a
/// success. Unlike Binance, POST sends a JSON body (Bybit V5 is JSON-bodied) rather than a
/// form-encoded one, and the recv-window travels in a header (added by the signing handler)
/// rather than in the query string or body.
/// </summary>
internal sealed class BybitHttpClient(HttpClient httpClient) : IBybitHttpClient
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
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(endpoint, parameters));
        if (signed) BybitSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        // Bybit V5 POST is JSON-bodied; the signing handler signs the exact raw body string it
        // reads back, so the serialized JSON must be the wire body verbatim.
        var json = JsonSerializer.Serialize(parameters ?? [], JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (signed) BybitSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<T> DeleteAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUrl(endpoint, parameters));
        if (signed) BybitSigningRequest.MarkSigned(request);
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
