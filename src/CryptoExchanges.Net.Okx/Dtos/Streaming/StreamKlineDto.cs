namespace CryptoExchanges.Net.Okx.Dtos.Streaming;

/// <summary>
/// The <c>data</c> array of an OKX WebSocket kline frame (<c>channel: candle1m</c>, etc.) is a list
/// of positional string arrays: <c>[ts, open, high, low, close, vol, volCcy, volCcyQuote, confirm]</c>.
/// Symbol is sourced from <c>arg.instId</c> on the outer envelope.
/// </summary>
internal sealed record StreamKlineDto
{
    // OKX kline rows are positional arrays, not named objects — decoder reads by index.
    public List<string> Row { get; init; } = [];
}
