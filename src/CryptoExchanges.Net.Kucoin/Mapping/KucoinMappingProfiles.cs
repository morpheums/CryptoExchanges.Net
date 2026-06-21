using DeltaMapper;
using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Dtos.Streaming;
using CryptoExchanges.Net.Kucoin.Internal;

namespace CryptoExchanges.Net.Kucoin.Mapping;

/// <summary>
/// DeltaMapper profile mapping KuCoin V1/V2 response DTOs to venue-neutral domain models.
/// Decimal, enum, and timestamp fields are converted via <see cref="KucoinValueParsers"/> so behaviour
/// matches the hand-written projections elsewhere in the service exactly. Wire symbol strings are
/// resolved through the injected <see cref="ISymbolMapper"/>.
/// </summary>
internal sealed class KucoinResponseProfile : Profile
{
    /// <summary>
    /// Creates the profile, capturing the <paramref name="symbolMapper"/> used to resolve wire symbol
    /// strings into <see cref="Symbol"/> values inside the mapping expressions.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper used for wire-string resolution.</param>
    public KucoinResponseProfile(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);

        // OrderDto -> Order. KuCoin reports cumulative filled base (dealSize) and cumulative
        // filled quote (dealFunds) directly; market orders carry a zero price.
        CreateMap<OrderDto, Order>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.ClientOrderId, o => o.MapFrom(s => string.IsNullOrEmpty(s.ClientOid) ? null : s.ClientOid))
            .ForMember(d => d.Price, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.OriginalQuantity, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Size)))
            .ForMember(d => d.ExecutedQuantity, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.DealSize)))
            .ForMember(d => d.CumulativeQuoteQuantity, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.DealFunds)))
            .ForMember(d => d.Side, o => o.MapFrom(s => KucoinValueParsers.ParseOrderSide(s.Side)))
            .ForMember(d => d.Type, o => o.MapFrom(s => KucoinValueParsers.ParseOrderType(s.Type)))
            .ForMember(d => d.Status, o => o.MapFrom(s => KucoinValueParsers.ParseOrderStatus(s.IsActive, s.CancelExist)))
            .ForMember(d => d.TimeInForce, o => o.MapFrom(s => KucoinValueParsers.ParseTimeInForce(s.TimeInForce)))
            .ForMember(d => d.StopPrice, o => o.Ignore())
            .ForMember(d => d.IcebergQuantity, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => ParseMs(s.CreatedAt)))
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        // TickerDto -> Ticker. KuCoin reports open (price 24h ago); price change is last - open,
        // and the percent is that change over open * 100.
        CreateMap<TickerDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Last)))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.High)))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Low)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Vol)))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.VolValue)))
            .ForMember(d => d.PriceChange, o => o.MapFrom(s =>
                KucoinValueParsers.ParseDecimal(s.Last) - KucoinValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s =>
                KucoinValueParsers.ParseDecimal(s.Open) == 0m
                    ? 0m
                    : (KucoinValueParsers.ParseDecimal(s.Last) - KucoinValueParsers.ParseDecimal(s.Open))
                      / KucoinValueParsers.ParseDecimal(s.Open) * 100m))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => ParseMs(s.Time)));

        // SymbolInfoDto -> SymbolInfo. KuCoin exposes lot/tick precision via separate fields; the
        // numeric filter fields stay null pending a dedicated task.
        CreateMap<SymbolInfoDto, SymbolInfo>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromComponents(s.BaseCurrency, s.QuoteCurrency)))
            .ForMember(d => d.AllowedOrderTypes, o => o.MapFrom(s => DefaultSpotOrderTypes))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TickSize, o => o.Ignore())
            .ForMember(d => d.MinQuantity, o => o.Ignore())
            .ForMember(d => d.MaxQuantity, o => o.Ignore())
            .ForMember(d => d.StepSize, o => o.Ignore())
            .ForMember(d => d.MinNotional, o => o.Ignore());

        // BalanceDto -> AssetBalance. KuCoin reports available (free) + holds (locked) directly.
        // Long-tail coins map to Asset.None rather than throwing.
        CreateMap<BalanceDto, AssetBalance>()
            .ForMember(d => d.Asset, o => o.MapFrom(s => KucoinValueParsers.ParseAssetOrNone(s.Currency)))
            .ForMember(d => d.Free, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Available)))
            .ForMember(d => d.Locked, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Holds)));

        // FillDto -> Trade. KuCoin's fill record carries the symbol wire string, so the mapper
        // resolves it directly without requiring the caller to supply it.
        CreateMap<FillDto, Trade>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.Id, o => o.MapFrom(s => s.TradeId))
            .ForMember(d => d.Price, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.Quantity, o => o.MapFrom(s => KucoinValueParsers.ParseDecimal(s.Size)))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => ParseMs(s.CreatedAt)))
            // IsBuyerMaker: a buy fill that is the maker, or a sell fill that is the taker.
            .ForMember(d => d.IsBuyerMaker, o => o.MapFrom(s =>
                s.Side == "buy" ? s.Liquidity == "maker" : s.Liquidity != "maker"))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrderId));

        // StreamTickerDto -> Ticker. WebSocket snapshot frame (/market/snapshot) carries the
        // last traded price, 24h open/high/low, base/quote volumes, absolute and fractional
        // price change, and a unix-ms timestamp (JSON number). changeRate is a fraction
        // (e.g. 0.0014) so multiply by 100 to produce a percentage.
        CreateMap<StreamTickerDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => s.LastTradedPrice))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => s.Open))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => s.High))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => s.Low))
            .ForMember(d => d.Volume, o => o.MapFrom(s => s.Vol))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => s.VolValue))
            .ForMember(d => d.PriceChange, o => o.MapFrom(s => s.ChangePrice))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s => s.ChangeRate * 100m))
            // datetime is unix milliseconds (JSON number) — convert directly.
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => s.Datetime > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(s.Datetime)
                : (DateTimeOffset?)null));
    }

    /// <summary>The order types KuCoin spot accepts (stop/take-profit use the separate plan API).</summary>
    private static readonly OrderType[] DefaultSpotOrderTypes = [OrderType.Limit, OrderType.Market];

    /// <summary>Parses a KuCoin string-encoded unix-ms timestamp into an optional instant (null when unset/zero).</summary>
    private static DateTimeOffset? ParseMs(string value) => ParseMs(KucoinValueParsers.ParseMs(value));

    /// <summary>Converts a unix-ms epoch long into an optional instant (null when zero).</summary>
    private static DateTimeOffset? ParseMs(long ms)
        => ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

}
