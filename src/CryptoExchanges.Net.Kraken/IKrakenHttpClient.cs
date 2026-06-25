namespace CryptoExchanges.Net.Kraken;

/// <summary>Internal HTTP surface the Kraken services depend on (enables typed-client DI + unit testing).</summary>
internal interface IKrakenHttpClient
{
    /// <summary>Sends a GET and deserializes the JSON response.</summary>
    Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = false, CancellationToken ct = default);

    /// <summary>Sends a POST whose body is <c>application/x-www-form-urlencoded</c> and deserializes the JSON response.</summary>
    Task<T> PostAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);

    /// <summary>
    /// Sends a form-encoded POST and returns the value of <paramref name="propertyKey"/> nested under the
    /// response's <c>result</c> envelope, deserialized as <typeparamref name="T"/>. Surfaces in-body
    /// <c>error[]</c> failures (HTTP 200) as typed exceptions before extraction; returns <c>default</c> when
    /// <c>result</c> or the property is absent.
    /// </summary>
    Task<T> PostResultPropertyAsync<T>(string endpoint, string propertyKey, Dictionary<string, string>? parameters = null, bool signed = true, CancellationToken ct = default);
}
