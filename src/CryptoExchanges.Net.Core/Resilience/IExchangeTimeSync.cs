namespace CryptoExchanges.Net.Core.Resilience;

/// <summary>Venue-neutral clock-skew offset (server − local, in ms) shared by all exchange clients.</summary>
public interface IExchangeTimeSync
{
    /// <summary>Returns <c>server − local</c>, in milliseconds.</summary>
    long ComputeOffset(long serverTimeMs, long localNowMs);

    /// <summary>Computes the offset and atomically writes it into <paramref name="offsetHolder"/>[0]; returns it.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="offsetHolder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="offsetHolder"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="serverTimeMs"/> is not positive.</exception>
    long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder);
}
