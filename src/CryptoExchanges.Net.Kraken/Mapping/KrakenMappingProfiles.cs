using DeltaMapper;
using CryptoExchanges.Net.Kraken.Dtos;
using CryptoExchanges.Net.Kraken.Internal;

namespace CryptoExchanges.Net.Kraken.Mapping;

/// <summary>
/// DeltaMapper profile mapping Kraken REST response DTOs to venue-neutral domain models.
/// Decimal, enum, and timestamp fields are converted via <see cref="KrakenValueParsers"/> so behaviour
/// matches the hand-written projections elsewhere in the service exactly. Wire symbol strings are
/// resolved through the injected <see cref="ISymbolMapper"/>.
/// </summary>
internal sealed class KrakenResponseProfile : Profile
{
    /// <summary>
    /// Creates the profile, capturing the <paramref name="symbolMapper"/> used to resolve wire symbol
    /// strings into <see cref="Symbol"/> values inside the mapping expressions.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper used for wire-string resolution.</param>
    public KrakenResponseProfile(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);

        // OrderDto -> Order. price in descr = limit price; order-level price = average fill price.
        CreateMap<OrderDto, Order>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Descr.Pair)))
            .ForMember(d => d.OrderId, o => o.Ignore())
            .ForMember(d => d.ClientOrderId, o => o.MapFrom(s => s.UserRef.HasValue ? s.UserRef.Value.ToString() : null))
            .ForMember(d => d.Price, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Descr.Price)))
            .ForMember(d => d.OriginalQuantity, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Vol)))
            .ForMember(d => d.ExecutedQuantity, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.VolExec)))
            .ForMember(d => d.CumulativeQuoteQuantity, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Cost)))
            .ForMember(d => d.Side, o => o.MapFrom(s => KrakenValueParsers.ParseOrderSide(s.Descr.Side)))
            .ForMember(d => d.Type, o => o.MapFrom(s => KrakenValueParsers.ParseOrderType(s.Descr.OrderType)))
            .ForMember(d => d.Status, o => o.MapFrom(s => KrakenValueParsers.ParseOrderStatus(s.Status)))
            .ForMember(d => d.TimeInForce, o => o.MapFrom(_ => TimeInForce.Gtc))
            .ForMember(d => d.StopPrice, o => o.Ignore())
            .ForMember(d => d.IcebergQuantity, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => ParseFractionalSeconds(s.OpenTime)))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(s => ParseFractionalSeconds(s.CloseTime)));

        // TickerDto -> Ticker. Positional arrays: [0] = today, [1] = last 24h. Symbol is set by caller.
        CreateMap<TickerDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.Ignore())
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Close.Count > 0 ? s.Close[0] : "0")))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.High.Count > 1 ? s.High[1] : "0")))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Low.Count > 1 ? s.Low[1] : "0")))
            .ForMember(d => d.Volume, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Volume.Count > 1 ? s.Volume[1] : "0")))
            .ForMember(d => d.QuoteVolume, o => o.Ignore())
            .ForMember(d => d.PriceChange, o => o.MapFrom(s =>
                KrakenValueParsers.ParseDecimal(s.Close.Count > 0 ? s.Close[0] : "0")
                - KrakenValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s =>
                KrakenValueParsers.ParseDecimal(s.Open) == 0m
                    ? 0m
                    : (KrakenValueParsers.ParseDecimal(s.Close.Count > 0 ? s.Close[0] : "0")
                       - KrakenValueParsers.ParseDecimal(s.Open))
                      / KrakenValueParsers.ParseDecimal(s.Open) * 100m))
            .ForMember(d => d.Timestamp, o => o.Ignore());

        // SymbolInfoDto -> SymbolInfo. Symbol resolved from wsname (slash-delimited wire form).
        CreateMap<SymbolInfoDto, SymbolInfo>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Wsname)))
            .ForMember(d => d.AllowedOrderTypes, o => o.MapFrom(s => DefaultSpotOrderTypes))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TickSize, o => o.Ignore())
            .ForMember(d => d.MinQuantity, o => o.MapFrom(s => KrakenValueParsers.ParseOptionalDecimal(s.OrderMin)))
            .ForMember(d => d.MaxQuantity, o => o.Ignore())
            .ForMember(d => d.StepSize, o => o.Ignore())
            .ForMember(d => d.MinNotional, o => o.Ignore());

        // BalanceDto -> AssetBalance. /Balance returns no locked amount; locked is always 0.
        CreateMap<BalanceDto, AssetBalance>()
            .ForMember(d => d.Asset, o => o.MapFrom(s => KrakenValueParsers.ParseAssetOrNone(s.Asset)))
            .ForMember(d => d.Free, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Balance)))
            .ForMember(d => d.Locked, o => o.MapFrom(_ => 0m));

        // FillDto -> Trade. IsBuyerMaker: true when the buy side is the maker (Maker bool on the fill).
        CreateMap<FillDto, Trade>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Pair)))
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Price, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.Quantity, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Volume)))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => ParseFractionalSeconds(s.Time)))
            .ForMember(d => d.IsBuyerMaker, o => o.MapFrom(s => s.Side == "buy" ? s.Maker : !s.Maker))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrderTxId));

        // CandlestickDto -> Candlestick. Kraken OHLC uses unix seconds; symbol/interval set by service.
        CreateMap<CandlestickDto, Candlestick>()
            .ForMember(d => d.OpenTime, o => o.MapFrom(s => DateTimeOffset.FromUnixTimeSeconds(s.OpenTime)))
            .ForMember(d => d.CloseTime, o => o.Ignore())
            .ForMember(d => d.Open, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.High, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.High)))
            .ForMember(d => d.Low, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Low)))
            .ForMember(d => d.Close, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Close)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => KrakenValueParsers.ParseDecimal(s.Volume)))
            .ForMember(d => d.QuoteVolume, o => o.Ignore())
            .ForMember(d => d.TradeCount, o => o.MapFrom(s => s.Count))
            .ForMember(d => d.Interval, o => o.Ignore())
            .ForMember(d => d.TradingSymbol, o => o.Ignore());
    }

    /// <summary>The order types Kraken spot accepts.</summary>
    private static readonly OrderType[] DefaultSpotOrderTypes = [OrderType.Limit, OrderType.Market];

    /// <summary>Parses a Kraken fractional-seconds timestamp into an optional instant (null when zero).</summary>
    private static DateTimeOffset? ParseFractionalSeconds(decimal value)
    {
        if (value <= 0m)
            return null;
        var ms = KrakenValueParsers.ParseFractionalSecondsToMs(value);
        return ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;
    }
}
