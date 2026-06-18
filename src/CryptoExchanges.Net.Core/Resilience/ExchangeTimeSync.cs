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
        var offset = ComputeOffset(serverTimeMs, localNowMs);
        Interlocked.Exchange(ref offsetHolder[0], offset);
        return offset;
    }
}
