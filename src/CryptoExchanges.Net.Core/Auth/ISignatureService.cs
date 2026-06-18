namespace CryptoExchanges.Net.Core.Auth;

/// <summary>Signs a request payload, returning the encoded signature the signing handler places on the wire.</summary>
public interface ISignatureService
{
    /// <summary>Signs <paramref name="payload"/> and returns the encoded signature.</summary>
    string Sign(string payload);
}
