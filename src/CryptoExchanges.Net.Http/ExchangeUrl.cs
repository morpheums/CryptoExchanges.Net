using System.Text;

namespace CryptoExchanges.Net.Http;

/// <summary>
/// Shared URL helpers for exchange HTTP clients: building the escaped query string and validating that
/// a configured base URL is a host root. Centralized so every exchange produces byte-identical query
/// strings and enforces the same host-root invariant (the signed prehash on OKX/Bitget is rebuilt from
/// <c>RequestUri.AbsolutePath</c>/<c>Query</c>, so a base URL carrying a path segment would silently
/// break signature byte-consistency).
/// </summary>
internal static class ExchangeUrl
{
    /// <summary>
    /// Builds an <c>&amp;</c>-joined, percent-escaped query string from <paramref name="parameters"/> in
    /// iteration order (keys and values escaped via <see cref="Uri.EscapeDataString(string)"/>). Returns
    /// an empty string for null/empty input.
    /// </summary>
    public static string BuildQueryString(IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kvp.Key)).Append('=').Append(Uri.EscapeDataString(kvp.Value));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Validates that <paramref name="baseUrl"/> is an absolute host root with NO path segment, then trims
    /// a trailing slash. Exchanges whose signed prehash is reassembled from <c>RequestUri.AbsolutePath</c>/
    /// <c>Query</c> (OKX, Bitget) require this: a base URL carrying a path would shift those and break the
    /// sign-consistency invariant, so this fails fast rather than producing rejected signatures at runtime.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="baseUrl"/> is null/blank or carries a path segment.</exception>
    public static string NormalizeHostRoot(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var uri = new Uri(baseUrl, UriKind.Absolute);
        if (uri.AbsolutePath is not ("/" or ""))
            throw new ArgumentException(
                $"BaseUrl must be a host root with no path segment (got '{uri.AbsolutePath}'); the signed " +
                "prehash is rebuilt from RequestUri.AbsolutePath/Query and a path prefix would break it.",
                nameof(baseUrl));
        return baseUrl.TrimEnd('/');
    }
}
