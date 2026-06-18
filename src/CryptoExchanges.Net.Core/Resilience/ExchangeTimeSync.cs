namespace CryptoExchanges.Net.Core.Resilience;

/// <inheritdoc cref="IExchangeTimeSync" />
public sealed class ExchangeTimeSync : IExchangeTimeSync
{
    /// <inheritdoc />
    public long ComputeOffset(long serverTimeMs, long localNowMs) => serverTimeMs - localNowMs;

    /// <inheritdoc />
    public long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(offsetHolder);
        if (offsetHolder.Length < 1)
            throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));
        // A missing/unparseable server time (0 or negative) would write a ~-localNow offset and break
        // every subsequent signed request — reject it rather than corrupt the holder.
        if (serverTimeMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(serverTimeMs), serverTimeMs, "Server time must be positive.");
        var offset = ComputeOffset(serverTimeMs, localNowMs);
        Interlocked.Exchange(ref offsetHolder[0], offset);
        return offset;
    }
}
