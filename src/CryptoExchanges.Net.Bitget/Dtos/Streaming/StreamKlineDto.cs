namespace CryptoExchanges.Net.Bitget.Dtos.Streaming;

/// <summary>
/// A single kline row from a Bitget v2 WebSocket candle frame (<c>channel: candle1m</c>, etc.).
/// Each <c>data</c> element is a positional string array: index 0=ts, 1=open, 2=high, 3=low, 4=close, 5=baseVol, 6=quoteVol.
/// Symbol is resolved from <c>arg.instId</c> on the outer envelope.
/// </summary>
internal sealed record StreamKlineDto
{
    /// <summary>Raw positional candle row.</summary>
    public List<string> Row { get; init; } = [];
}
