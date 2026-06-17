namespace CryptoExchanges.Net.Http;

/// <summary>
/// A no-op <see cref="DelegatingHandler"/> used as a request-finalizer placeholder when no
/// finalizer is needed at resolution time (e.g. a Binance client configured without a secret key,
/// so no signing handler should run). Options are only final when the typed client is resolved,
/// so the choice between a real finalizer and this pass-through is deferred to that point.
/// </summary>
public sealed class PassThroughHandler : DelegatingHandler
{
}
