namespace CryptoExchanges.Net.Kucoin;

/// <summary>Internal HTTP surface the KuCoin services depend on (enables typed-client DI + unit testing).</summary>
internal interface IKucoinHttpClient
{
    /// <summary>Sends a GET and deserializes the JSON response.</summary>
    Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a POST (flat JSON-object body) and deserializes the JSON response.</summary>
    Task<T> PostAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);

    /// <summary>
    /// Sends a POST whose body is an arbitrary object (e.g. a JSON array for batch endpoints) and
    /// deserializes the JSON response. The serialized body is the verbatim wire body the signer reads.
    /// </summary>
    Task<T> PostAsync<T>(string endpoint, object body, bool signed = true, CancellationToken ct = default);

    /// <summary>Sends a DELETE and deserializes the JSON response.</summary>
    Task<T> DeleteAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);
}
