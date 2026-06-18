namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>Base type for all exchange-related failures surfaced by the SDK.</summary>
public abstract class ExchangeException : Exception
{
    /// <summary>Creates an exchange exception.</summary>
    protected ExchangeException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
