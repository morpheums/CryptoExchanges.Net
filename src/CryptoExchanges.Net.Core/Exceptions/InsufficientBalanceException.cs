namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>The account had insufficient balance for the requested operation.</summary>
public sealed class InsufficientBalanceException : ExchangeApiException
{
    /// <summary>Creates an insufficient-balance exception.</summary>
    public InsufficientBalanceException(string message, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, code, rawBody, innerException) { }
}
