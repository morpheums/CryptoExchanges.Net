namespace CryptoExchanges.Net.Bybit;

/// <summary>Internal HTTP surface the Bybit services depend on (enables typed-client DI + unit testing).</summary>
internal interface IBybitHttpClient
{
    /// <summary>Sends a GET and deserializes the JSON response.</summary>
    Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a POST (JSON body) and deserializes the JSON response.</summary>
    Task<T> PostAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);

    /// <summary>Sends a DELETE and deserializes the JSON response.</summary>
    Task<T> DeleteAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);
}
