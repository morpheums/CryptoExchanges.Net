namespace CryptoExchanges.Net.Http;

/// <summary>Reads the <c>Retry-After</c> delay from a response, supporting both the
/// delta-seconds and the absolute HTTP-date forms.</summary>
public static class RetryAfterReader
{
    /// <summary>The indicated wait, or null when no usable Retry-After is present.</summary>
    public static TimeSpan? GetDelay(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var ra = response.Headers.RetryAfter;
        if (ra is null) return null;
        if (ra.Delta is { } delta) return delta > TimeSpan.Zero ? delta : null;
        if (ra.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : null;
        }
        return null;
    }
}
