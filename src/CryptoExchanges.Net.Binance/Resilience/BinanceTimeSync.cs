namespace CryptoExchanges.Net.Binance.Resilience;

/// <summary>Computes the clock-skew offset (server - local, in ms) applied to signed-request
/// timestamps so a skewed local clock does not trip Binance's recvWindow (-1021).</summary>
public static class BinanceTimeSync
{
    /// <summary>server - local, in milliseconds.</summary>
    public static long ComputeOffset(long serverTimeMs, long localNowMs) => serverTimeMs - localNowMs;
}
