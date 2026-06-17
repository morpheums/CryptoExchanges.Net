namespace CryptoExchanges.Net.Bybit.Resilience;

/// <summary>Computes the clock-skew offset (server - local, in ms) applied to signed-request
/// timestamps so a skewed local clock does not trip Bybit's recv-window check (retCode 10002).
/// The offset is read from Bybit's server-time endpoint (<c>/v5/market/time</c>, whose
/// <c>result.timeNano</c>/<c>result.timeSecond</c> envelope carries server time in milliseconds).</summary>
public static class BybitTimeSync
{
    /// <summary>server - local, in milliseconds.</summary>
    public static long ComputeOffset(long serverTimeMs, long localNowMs) => serverTimeMs - localNowMs;

    /// <summary>Computes the offset and writes it atomically into the shared single-element holder
    /// that the signing handler reads on each request. Returns the offset that was written.</summary>
    public static long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(offsetHolder);
        var offset = ComputeOffset(serverTimeMs, localNowMs);
        Interlocked.Exchange(ref offsetHolder[0], offset);
        return offset;
    }
}
