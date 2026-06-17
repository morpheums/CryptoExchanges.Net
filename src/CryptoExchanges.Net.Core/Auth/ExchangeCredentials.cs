namespace CryptoExchanges.Net.Core.Auth;

/// <summary>
/// API credentials for an exchange. Carries the API key, the HMAC secret, and an optional
/// passphrase. The passphrase is <see langword="null"/> for exchanges that do not use one
/// (e.g. Binance, Bybit) and present for those that do (e.g. OKX, Bitget).
/// </summary>
/// <remarks>
/// Per ADR-001, secrets must not leak via logging. <see cref="ToString"/> is overridden to
/// REDACT <see cref="SecretKey"/> and <see cref="Passphrase"/> and to mask <see cref="ApiKey"/>,
/// so an accidental interpolation or log call never emits raw credential values.
/// </remarks>
public sealed record ExchangeCredentials
{
    /// <summary>The public API key.</summary>
    public string ApiKey { get; }

    /// <summary>The HMAC secret used to sign requests.</summary>
    public string SecretKey { get; }

    /// <summary>
    /// The optional API passphrase. <see langword="null"/> when the exchange does not require one
    /// (Binance, Bybit); a non-empty value when it does (OKX, Bitget).
    /// </summary>
    public string? Passphrase { get; }

    /// <summary>
    /// Creates a credential set.
    /// </summary>
    /// <param name="apiKey">The public API key. Must be non-empty.</param>
    /// <param name="secretKey">The HMAC secret. Must be non-empty.</param>
    /// <param name="passphrase">
    /// The optional API passphrase. Pass <see langword="null"/> (the default) for exchanges that do
    /// not use a passphrase. When supplied it must be non-whitespace.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="apiKey"/> or <paramref name="secretKey"/> is null/empty/whitespace, or a
    /// non-null <paramref name="passphrase"/> is empty/whitespace.
    /// </exception>
    public ExchangeCredentials(string apiKey, string secretKey, string? passphrase = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        if (passphrase is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);

        ApiKey = apiKey;
        SecretKey = secretKey;
        Passphrase = passphrase;
    }

    /// <summary>True when a passphrase is present (OKX/Bitget-style credentials).</summary>
    public bool HasPassphrase => Passphrase is not null;

    /// <summary>
    /// Returns a redacted representation that never contains the raw <see cref="SecretKey"/> or
    /// <see cref="Passphrase"/>. The API key is masked to its last four characters.
    /// </summary>
    public override string ToString()
        => $"{nameof(ExchangeCredentials)} {{ ApiKey = {Mask(ApiKey)}, SecretKey = [REDACTED], "
            + $"Passphrase = {(HasPassphrase ? "[REDACTED]" : "(none)")} }}";

    private static string Mask(string value)
        => value.Length <= 4 ? "****" : $"****{value[^4..]}";
}
