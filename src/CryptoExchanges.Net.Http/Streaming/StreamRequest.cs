namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Venue-neutral, immutable descriptor for a single subscription request.
/// The protocol's <c>BuildSubscribe</c> / <c>BuildUnsubscribe</c> methods translate
/// this into the exchange wire format. Symbol resolution (from the canonical symbol type
/// to the exchange wire symbol) happens exchange-side <em>before</em> this record is
/// built — the Http layer receives only the resolved wire string (binding constraint K1).
/// </summary>
/// <param name="Kind">The type of market-data stream being requested.</param>
/// <param name="WireSymbol">
/// The already-resolved exchange wire symbol string (e.g. <c>"BTCUSDT"</c>).
/// </param>
/// <param name="Depth">
/// Optional order-book depth level. Non-null only for <see cref="StreamKind.OrderBook"/>
/// subscriptions; the protocol interprets the value per venue convention.
/// </param>
/// <param name="Interval">
/// Optional kline interval string. Non-null only for <see cref="StreamKind.Kline"/>
/// subscriptions; expressed in the exchange's native interval notation
/// (e.g. <c>"1m"</c>, <c>"1h"</c>).
/// </param>
internal sealed record StreamRequest(
    StreamKind Kind,
    string WireSymbol,
    int? Depth = null,
    string? Interval = null);
