using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Http;

/// <summary>
/// Outermost resilience handler: awaits the per-client <see cref="IRateLimitGate"/> before each
/// request and lets the gate observe the response (e.g. Retry-After / usage headers).
/// </summary>
public sealed class RateLimitThrottleHandler(IRateLimitGate gate) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        gate.Observe(response);
        return response;
    }
}
