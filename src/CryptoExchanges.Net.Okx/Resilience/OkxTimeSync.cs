namespace CryptoExchanges.Net.Okx.Resilience;

/// <summary>
/// Computes the clock-skew offset (server - local, in ms) applied to signed-request timestamps so a
/// skewed local clock does not trip OKX's timestamp-expiry check. The offset is read from OKX's
/// server-time endpoint (<c>/api/v5/public/time</c>, whose <c>data[0].ts</c> is epoch-ms as a string).
/// </summary>
/// <remarks>
/// Internal per ADR-001 conv #2 (the in-assembly composer/client construct + read this directly). The
/// offset holder works in milliseconds; the OKX signing handler does <c>UtcNow.AddMilliseconds(offset)</c>
/// then formats the result as the ISO-8601 timestamp OKX expects, so this stays consistent with Bybit.
/// </remarks>
internal static class OkxTimeSync
{
    /// <summary>server - local, in milliseconds.</summary>
    public static long ComputeOffset(long serverTimeMs, long localNowMs) => serverTimeMs - localNowMs;

    /// <summary>Computes the offset and writes it atomically into the shared single-element holder that
    /// the signing handler reads on each request. Returns the offset that was written.</summary>
    public static long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(offsetHolder);
        if (offsetHolder.Length < 1)
            throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));
        var offset = ComputeOffset(serverTimeMs, localNowMs);
        Interlocked.Exchange(ref offsetHolder[0], offset);
        return offset;
    }
}
