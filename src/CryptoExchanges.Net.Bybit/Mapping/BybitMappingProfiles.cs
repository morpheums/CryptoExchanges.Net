using DeltaMapper;
using CryptoExchanges.Net.Bybit.Dtos.Streaming;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Bybit.Services;

namespace CryptoExchanges.Net.Bybit.Mapping;

/// <summary>
/// DeltaMapper profile mapping Bybit V5 response DTOs to venue-neutral domain models.
/// Decimal, enum, and timestamp fields are converted via <see cref="BybitValueParsers"/> so
/// behaviour matches the hand-written projections elsewhere in the service exactly. Wire symbol
/// strings are resolved through the injected <see cref="ISymbolMapper"/>.
/// </summary>
internal sealed class BybitResponseProfile : Profile
{
    /// <summary>
    /// Creates the profile, capturing the <paramref name="symbolMapper"/> used to resolve
    /// wire symbol strings into <see cref="Symbol"/> values inside the mapping expressions.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper used for wire-string resolution.</param>
    public BybitResponseProfile(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);

        CreateMap<OrderDto, Order>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrderId))
            .ForMember(d => d.ClientOrderId, o => o.MapFrom(s => string.IsNullOrEmpty(s.OrderLinkId) ? null : s.OrderLinkId))
            .ForMember(d => d.Price, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.OriginalQuantity, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Qty)))
            .ForMember(d => d.ExecutedQuantity, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.CumExecQty)))
            .ForMember(d => d.Side, o => o.MapFrom(s => BybitValueParsers.ParseOrderSide(s.Side)))
            .ForMember(d => d.Type, o => o.MapFrom(s => BybitValueParsers.ParseOrderType(s.OrderType)))
            .ForMember(d => d.Status, o => o.MapFrom(s => BybitValueParsers.ParseOrderStatus(s.OrderStatus)))
            .ForMember(d => d.TimeInForce, o => o.MapFrom(s => BybitValueParsers.ParseTimeInForce(s.TimeInForce)))
            .ForMember(d => d.StopPrice, o => o.MapFrom(s => BybitValueParsers.ParseOptionalDecimal(s.TriggerPrice)))
            .ForMember(d => d.IcebergQuantity, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => ParseTimestamp(s.CreatedTime)))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(s => ParseTimestamp(s.UpdatedTime)))
            .ForMember(d => d.CumulativeQuoteQuantity, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.CumExecValue)));

        // StreamTickerDto -> Ticker (WebSocket ticker push frame data payload).
        // price24hPcnt is a fraction (0.01 = +1%); scale to a percent. Bybit v5 ticker push
        // does not include a timestamp field, so Timestamp is left null.
        CreateMap<StreamTickerDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.LastPrice)))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.PrevPrice24h)))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.HighPrice24h)))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.LowPrice24h)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Volume24h)))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Turnover24h)))
            .ForMember(d => d.PriceChange, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.LastPrice) - BybitValueParsers.ParseDecimal(s.PrevPrice24h)))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Price24hPcnt) * 100m))
            .ForMember(d => d.Timestamp, o => o.Ignore());

        // TickerDto -> Ticker. price24hPcnt is a fraction (0.01 = +1%); scale to a percent.
        CreateMap<TickerDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.LastPrice)))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.PrevPrice24h)))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.HighPrice24h)))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.LowPrice24h)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Volume24h)))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Turnover24h)))
            .ForMember(d => d.PriceChange, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.LastPrice) - BybitValueParsers.ParseDecimal(s.PrevPrice24h)))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Price24hPcnt) * 100m))
            .ForMember(d => d.Timestamp, o => o.Ignore());

        // SymbolInfoDto -> SymbolInfo (instruments-info symbol projection; explicit base/quote legs).
        // Bybit V5 instruments-info exposes lot/price filters under nested objects that this SDK does
        // not yet surface; the numeric filter fields stay null pending a dedicated filters task.
        CreateMap<SymbolInfoDto, SymbolInfo>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromComponents(s.BaseCoin, s.QuoteCoin)))
            .ForMember(d => d.AllowedOrderTypes, o => o.MapFrom(s => DefaultSpotOrderTypes))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TickSize, o => o.Ignore())
            .ForMember(d => d.MinQuantity, o => o.Ignore())
            .ForMember(d => d.MaxQuantity, o => o.Ignore())
            .ForMember(d => d.StepSize, o => o.Ignore())
            .ForMember(d => d.MinNotional, o => o.Ignore());

        // BalanceDto -> AssetBalance. Bybit reports total walletBalance + locked; the free
        // portion is the difference. Long-tail coins map to Asset.None rather than throwing.
        CreateMap<BalanceDto, AssetBalance>()
            .ForMember(d => d.Asset, o => o.MapFrom(s => BybitValueParsers.ParseAssetOrNone(s.Coin)))
            .ForMember(d => d.Free, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.WalletBalance) - BybitValueParsers.ParseDecimal(s.Locked)))
            .ForMember(d => d.Locked, o => o.MapFrom(s => BybitValueParsers.ParseDecimal(s.Locked)));
    }

    /// <summary>The order types Bybit V5 spot accepts (stop/take-profit ride on a trigger field, not a type).</summary>
    private static readonly OrderType[] DefaultSpotOrderTypes = [OrderType.Limit, OrderType.Market];

    /// <summary>Parses a Bybit string-encoded unix-ms timestamp into an optional instant (null when unset/zero).</summary>
    private static DateTimeOffset? ParseTimestamp(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ms)
            || ms <= 0)
            return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }
}
