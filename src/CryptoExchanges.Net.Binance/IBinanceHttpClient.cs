namespace CryptoExchanges.Net.Binance;

/// <summary>Internal HTTP surface the Binance services depend on (enables typed-client DI + unit testing).</summary>
internal interface IBinanceHttpClient
{
    /// <summary>Sends a GET and deserializes the JSON response.</summary>
    Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a GET and returns the raw string response.</summary>
    Task<string> GetStringAsync(string endpoint, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a POST (form-encoded) and deserializes the JSON response.</summary>
    Task<T> PostAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);

    /// <summary>Sends a DELETE and deserializes the JSON response.</summary>
    Task<T> DeleteAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);
}
