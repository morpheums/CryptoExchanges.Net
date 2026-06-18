namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>The exchange rejected an order (parameters, filters, or unknown order).</summary>
public sealed class InvalidOrderException : ExchangeApiException
{
    /// <summary>Creates an invalid-order exception.</summary>
    public InvalidOrderException(string message, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, code, rawBody, innerException) { }
}
