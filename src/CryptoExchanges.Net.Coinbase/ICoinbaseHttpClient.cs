namespace CryptoExchanges.Net.Coinbase;

/// <summary>Internal HTTP surface the Coinbase services depend on (enables typed-client DI + unit testing).</summary>
internal interface ICoinbaseHttpClient
{
    /// <summary>Sends a GET and deserializes the JSON response.</summary>
    Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a POST (flat JSON-object body) and deserializes the JSON response.</summary>
    Task<T> PostAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);

    /// <summary>
    /// Sends a POST whose body is an arbitrary object and deserializes the JSON response.
    /// The serialized body is the verbatim wire body the signer reads.
    /// </summary>
    Task<T> PostAsync<T>(string endpoint, object body, bool signed = true, CancellationToken ct = default);

    /// <summary>Sends a DELETE and deserializes the JSON response.</summary>
    Task<T> DeleteAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);

    /// <summary>Sends a GET and returns the named JSON property (Coinbase's per-endpoint envelope) deserialized as <typeparamref name="T"/>; <c>default</c> when absent.</summary>
    Task<T> GetPropertyAsync<T>(string endpoint, string propertyKey, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a POST with <paramref name="body"/> and returns the named JSON property (Coinbase's per-endpoint envelope) deserialized as <typeparamref name="T"/>; <c>default</c> when absent.</summary>
    Task<T> PostPropertyAsync<T>(string endpoint, string propertyKey, object body, bool signed = true, CancellationToken ct = default);
}
