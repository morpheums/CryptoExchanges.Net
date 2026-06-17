namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>
/// Per-client gate that paces outbound requests against an exchange's rate limits.
/// Scoped to a single exchange-client instance (limits are enforced per API-key/IP).
/// The M1 implementation is reactive only; a proactive weight-window implementation is a
/// fast-follow that plugs into this same seam.
/// </summary>
public interface IRateLimitGate
{
    /// <summary>Awaited before each outbound request; delays when the gate requires it.</summary>
    ValueTask WaitAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates gate state from a received response (e.g. usage headers / Retry-After).</summary>
    void Observe(HttpResponseMessage response);
}
