using System.Globalization;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Binance.Resilience;

/// <summary>
/// Adds the Binance API-key header to every request, and for signed requests (re)computes the
/// timestamp + HMAC signature ON EACH ATTEMPT. Sits below the retry strategy so a retried,
/// delayed request is re-signed with a fresh timestamp (avoids -1021 recvWindow errors).
/// Supports query-signed (GET/DELETE) and body-signed (POST form) requests.
/// </summary>
internal sealed class BinanceSigningHandler(
    string apiKey, ISignatureService signatureService, Func<long> timeOffset) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Remove("X-MBX-APIKEY");
            request.Headers.Add("X-MBX-APIKEY", apiKey);
        }

        if (BinanceSigningRequest.IsSigned(request))
            await ResignAsync(request, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task ResignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeOffset())
            .ToString(CultureInfo.InvariantCulture);

        if (request.Method == HttpMethod.Post && request.Content is not null)
        {
            var raw = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var unsigned = StripSigning(raw);
            var withTs = Append(unsigned, $"timestamp={timestamp}");
            var signed = BuildSignedQuery(withTs);
            var previous = request.Content;
            request.Content = new StringContent(signed, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
            previous.Dispose();
        }
        else
        {
            var uri = request.RequestUri!;
            string path;
            string query;

            if (uri.IsAbsoluteUri)
            {
                path = uri.GetLeftPart(UriPartial.Path);
                query = uri.Query.TrimStart('?');
            }
            else
            {
                var uriStr = uri.OriginalString;
                var qIndex = uriStr.IndexOf('?', StringComparison.Ordinal);
                if (qIndex >= 0)
                {
                    path = uriStr[..qIndex];
                    query = uriStr[(qIndex + 1)..];
                }
                else
                {
                    path = uriStr;
                    query = string.Empty;
                }
            }

            var unsigned = StripSigning(query);
            var withTs = Append(unsigned, $"timestamp={timestamp}");
            var signed = BuildSignedQuery(withTs);
            var newUriStr = path + "?" + signed;
            request.RequestUri = new Uri(newUriStr, uri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
        }
    }

    // Binance carries the signature inline as a query parameter, so build the signed query here from
    // the interface's Sign (the concrete service's BuildSignedQuery is intentionally off ISignatureService).
    private string BuildSignedQuery(string queryString)
    {
        var signature = signatureService.Sign(queryString);
        var separator = string.IsNullOrEmpty(queryString) ? string.Empty : "&";
        return $"{queryString}{separator}signature={signature}";
    }

    private static string Append(string a, string b)
        => string.IsNullOrEmpty(a) ? b : $"{a}&{b}";

    private static string StripSigning(string query)
    {
        if (string.IsNullOrEmpty(query)) return query;
        var kept = query.Split('&')
            .Where(p => p.Length > 0
                && !p.StartsWith("timestamp=", StringComparison.Ordinal)
                && !p.StartsWith("signature=", StringComparison.Ordinal));
        return string.Join('&', kept);
    }
}
