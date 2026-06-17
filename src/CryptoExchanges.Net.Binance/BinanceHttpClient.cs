using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoExchanges.Net.Binance.Resilience;

namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Internal HTTP wrapper for Binance REST. Builds requests and deserializes successful
/// responses. Signing (timestamp + HMAC), the API-key header, retries, rate-limit handling,
/// and typed error translation are all provided by the resilience pipeline on the injected
/// <see cref="HttpClient"/>, so any response that reaches this type is already a success.
/// </summary>
internal sealed class BinanceHttpClient(HttpClient httpClient, decimal receiveWindow = 5000m)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Sends a GET and deserializes the JSON response.</summary>
    public async Task<T> GetAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = false, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(endpoint, parameters, signed));
        if (signed) BinanceSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <summary>Sends a GET and returns the raw string response.</summary>
    public async Task<string> GetStringAsync(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = false, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(endpoint, parameters, signed));
        if (signed) BinanceSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Sends a POST with form-encoded parameters and deserializes the JSON response.</summary>
    public async Task<T> PostAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        var query = BuildBaseQuery(parameters, signed);
        using var content = new StringContent(query, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (signed) BinanceSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <summary>Sends a DELETE and deserializes the JSON response.</summary>
    public async Task<T> DeleteAsync<T>(
        string endpoint, Dictionary<string, string>? parameters = null,
        bool signed = true, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUrl(endpoint, parameters, signed));
        if (signed) BinanceSigningRequest.MarkSigned(request);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    private string BuildUrl(string endpoint, Dictionary<string, string>? parameters, bool signed)
    {
        var query = BuildBaseQuery(parameters, signed);
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }

    private string BuildBaseQuery(Dictionary<string, string>? parameters, bool signed)
    {
        var query = BuildQueryString(parameters);
        if (!signed) return query;
        var rw = "recvWindow=" + receiveWindow.ToString(CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(query) ? rw : $"{query}&{rw}";
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
