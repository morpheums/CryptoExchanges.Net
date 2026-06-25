using CryptoExchanges.Net.Coinbase.Auth;

namespace CryptoExchanges.Net.Coinbase.Resilience;

/// <summary>
/// For signed Coinbase requests, removes any prior <c>Authorization</c> header and re-mints a fresh JWT
/// on each attempt using the ACTUAL outgoing method + host + path so retried or delayed requests never
/// send an expired token. Sits below the retry strategy: each retry attempt gets its own 120-second JWT.
/// Credential-absent gating (passing unsigned requests straight through) is wired in TASK-094 (DI).
/// </summary>
/// <param name="signer">Produces a fresh compact JWT for each signing attempt.</param>
internal sealed class CoinbaseSigningHandler(CoinbaseJwtSigner signer) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (CoinbaseSigningRequest.IsSigned(request))
            Resign(request);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void Resign(HttpRequestMessage request)
    {
        var method = request.Method.Method;
        var host = request.RequestUri!.Host;
        var pathAndQuery = request.RequestUri.PathAndQuery;

        var jwt = signer.MintJwt(method, host, pathAndQuery);

        request.Headers.Remove("Authorization");
        request.Headers.Add("Authorization", $"Bearer {jwt}");
    }
}
