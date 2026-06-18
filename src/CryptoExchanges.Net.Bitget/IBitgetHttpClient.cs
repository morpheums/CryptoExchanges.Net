namespace CryptoExchanges.Net.Bitget;

/// <summary>Internal HTTP surface the Bitget services depend on (enables typed-client DI + unit testing).</summary>
internal interface IBitgetHttpClient
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
