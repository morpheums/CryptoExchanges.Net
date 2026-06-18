namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>
/// A transport/connectivity failure that persisted after retries were exhausted.
/// For a failed mutation (e.g. order placement) the operation's outcome may be unknown.
/// </summary>
public sealed class ExchangeConnectivityException : ExchangeException
{
    /// <summary>True when a mutating request failed and its server-side outcome is unknown.</summary>
    public bool OperationOutcomeIndeterminate { get; }

    /// <summary>Creates a connectivity exception.</summary>
    public ExchangeConnectivityException(
        string message, bool operationOutcomeIndeterminate = false, Exception? innerException = null)
        : base(message, innerException) => OperationOutcomeIndeterminate = operationOutcomeIndeterminate;
}
