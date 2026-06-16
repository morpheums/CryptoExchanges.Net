using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Internal HTTP client wrapper for Binance REST API.
/// Handles request building, JSON serialization, and error mapping.
/// </summary>
internal sealed class BinanceHttpClient(
    HttpClient httpClient,
    string apiKey,
    BinanceSignatureService? signatureService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Clock-skew offset in milliseconds. Applied to every signed request timestamp.
    /// Use a positive value when the local clock is ahead of Binance servers;
    /// negative when it is behind.
    /// </summary>
    public long TimeOffset { get; set; }

    /// <summary>
    /// Sends a GET request to the specified endpoint and deserializes the JSON response.
    /// </summary>
    public async Task<T> GetAsync<T>(
        string endpoint,
        Dictionary<string, string>? parameters = null,
        bool signed = false,
        CancellationToken ct = default)
    {
        if (signed && signatureService is null)
            throw new InvalidOperationException("A signature service is required for signed requests.");

        var url = BuildUrl(endpoint, parameters, signed);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request);

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Sends a GET request and returns the raw string response.
    /// </summary>
    public async Task<string> GetStringAsync(
        string endpoint,
        Dictionary<string, string>? parameters = null,
        bool signed = false,
        CancellationToken ct = default)
    {
        if (signed && signatureService is null)
            throw new InvalidOperationException("A signature service is required for signed requests.");

        var url = BuildUrl(endpoint, parameters, signed);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request);

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request with form-encoded parameters and deserializes the JSON response.
    /// </summary>
    public async Task<T> PostAsync<T>(
        string endpoint,
        Dictionary<string, string>? parameters = null,
        bool signed = true,
        CancellationToken ct = default)
    {
        if (signed && signatureService is null)
            throw new InvalidOperationException("A signature service is required for signed requests.");

        var queryString = BuildQueryString(parameters);
        if (signed)
        {
            queryString = AppendTimestamp(queryString);
            queryString = signatureService!.BuildSignedQuery(queryString);
        }

        using var content = new StringContent(queryString, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        AddHeaders(request);

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Sends a DELETE request and deserializes the JSON response.
    /// </summary>
    public async Task<T> DeleteAsync<T>(
        string endpoint,
        Dictionary<string, string>? parameters = null,
        bool signed = true,
        CancellationToken ct = default)
    {
        if (signed && signatureService is null)
            throw new InvalidOperationException("A signature service is required for signed requests.");

        var queryString = BuildQueryString(parameters);
        if (signed)
        {
            queryString = AppendTimestamp(queryString);
            queryString = signatureService!.BuildSignedQuery(queryString);
        }

        var url = $"{endpoint}?{queryString}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        AddHeaders(request);

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }

    // ── Helpers ──

    private string BuildUrl(string endpoint, Dictionary<string, string>? parameters, bool signed)
    {
        var queryString = BuildQueryString(parameters);
        if (signed)
        {
            queryString = AppendTimestamp(queryString);
            queryString = signatureService!.BuildSignedQuery(queryString);
        }
        return string.IsNullOrEmpty(queryString) ? endpoint : $"{endpoint}?{queryString}";
    }

    private static string BuildQueryString(Dictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kvp.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kvp.Value));
        }
        return sb.ToString();
    }

    private string AppendTimestamp(string queryString)
    {
        var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TimeOffset).ToString();
        var prefix = string.IsNullOrEmpty(queryString) ? string.Empty : "&";
        return $"{queryString}{prefix}timestamp={timestamp}";
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Add("X-MBX-APIKEY", apiKey);
    }

    /// <summary>
    /// Checks the HTTP response for Binance-specific error codes and throws on failure.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        string message;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("code", out var codeEl) &&
                doc.RootElement.TryGetProperty("msg", out var msgEl))
            {
                message = $"Binance error {codeEl.GetInt32()}: {msgEl.GetString()}";
            }
            else
            {
                message = $"Binance HTTP {(int)response.StatusCode}: {body}";
            }
        }
        catch
        {
            message = $"Binance HTTP {(int)response.StatusCode}: {body}";
        }

        throw new BinanceApiException(message, (int)response.StatusCode);
    }
}

/// <summary>
/// Exception thrown when Binance returns an API error.
/// </summary>
public sealed class BinanceApiException : Exception
{
    /// <summary>The HTTP status code returned by Binance.</summary>
    public int StatusCode { get; }

    public BinanceApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
