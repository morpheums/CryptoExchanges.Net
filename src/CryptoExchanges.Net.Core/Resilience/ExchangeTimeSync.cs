namespace CryptoExchanges.Net.Core.Resilience;

/// <summary>Venue-neutral clock-skew offset (server − local, in ms) shared by all exchange clients.</summary>
public static class ExchangeTimeSync
{
    /// <summary>Returns <c>server − local</c>, in milliseconds.</summary>
    public static long ComputeOffset(long serverTimeMs, long localNowMs) => serverTimeMs - localNowMs;

    /// <summary>Computes the offset and atomically writes it into <paramref name="offsetHolder"/>[0]; returns it.</summary>
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
