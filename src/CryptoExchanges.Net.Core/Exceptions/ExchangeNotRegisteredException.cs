using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>No exchange client was registered for the requested <see cref="ExchangeId"/>.</summary>
public sealed class ExchangeNotRegisteredException : ExchangeException
{
    /// <summary>The exchange identifier that had no registered client.</summary>
    public ExchangeId ExchangeId { get; }

    /// <summary>Creates an exception for an unregistered exchange.</summary>
    public ExchangeNotRegisteredException(ExchangeId exchangeId, Exception? innerException = null)
        : base($"No exchange client is registered for '{exchangeId}'. Did you call the matching Add*Exchange registration?", innerException)
        => ExchangeId = exchangeId;
}
