namespace CryptoExchanges.Net.Kraken;

/// <summary>Internal HTTP surface the Kraken services depend on (enables typed-client DI + unit testing).</summary>
internal interface IKrakenHttpClient
{
    /// <summary>Sends a GET and deserializes the JSON response.</summary>
    Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a POST whose body is <c>application/x-www-form-urlencoded</c> and deserializes the JSON response.</summary>
    Task<T> PostAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);
}
