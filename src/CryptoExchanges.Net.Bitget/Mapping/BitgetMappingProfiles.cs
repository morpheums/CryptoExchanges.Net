using DeltaMapper;
using CryptoExchanges.Net.Bitget.Internal;
using CryptoExchanges.Net.Bitget.Services;

namespace CryptoExchanges.Net.Bitget.Mapping;

/// <summary>
/// DeltaMapper profile mapping Bitget V2 response DTOs to venue-neutral domain models.
/// Decimal, enum, and timestamp fields are converted via <see cref="BitgetValueParsers"/> so behaviour
/// matches the hand-written projections elsewhere in the service exactly. Wire symbol strings are
/// resolved through the injected <see cref="ISymbolMapper"/>.
/// </summary>
internal sealed class BitgetResponseProfile : Profile
{
    /// <summary>
    /// Creates the profile, capturing the <paramref name="symbolMapper"/> used to resolve wire symbol
    /// strings into <see cref="Symbol"/> values inside the mapping expressions.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper used for wire-string resolution.</param>
    public BitgetResponseProfile(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);

        // BitgetOrder -> Order. Bitget reports cumulative filled base (baseVolume) and cumulative
        // filled quote (quoteVolume) directly, so no avgPx multiplication is needed.
        CreateMap<BitgetOrder, Order>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrderId))
            .ForMember(d => d.ClientOrderId, o => o.MapFrom(s => string.IsNullOrEmpty(s.ClientOid) ? null : s.ClientOid))
            .ForMember(d => d.Price, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.OriginalQuantity, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.Size)))
            .ForMember(d => d.ExecutedQuantity, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.BaseVolume)))
            .ForMember(d => d.CumulativeQuoteQuantity, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.QuoteVolume)))
            .ForMember(d => d.Side, o => o.MapFrom(s => BitgetValueParsers.ParseOrderSide(s.Side)))
            .ForMember(d => d.Type, o => o.MapFrom(s => BitgetValueParsers.ParseOrderType(s.OrderType)))
            .ForMember(d => d.Status, o => o.MapFrom(s => BitgetValueParsers.ParseOrderStatus(s.Status)))
            .ForMember(d => d.TimeInForce, o => o.MapFrom(s => BitgetValueParsers.ParseTimeInForce(s.Force)))
            .ForMember(d => d.StopPrice, o => o.Ignore())
            .ForMember(d => d.IcebergQuantity, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => ParseTimestamp(s.CTime)))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(s => ParseTimestamp(s.UTime)));

        // BitgetTicker -> Ticker. Bitget reports the fractional 24h change (change24h) directly, so
        // the percent is change24h * 100 and the absolute change is last - open.
        CreateMap<BitgetTicker, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.LastPr)))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.High24h)))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.Low24h)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.BaseVolume)))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.QuoteVolume)))
            .ForMember(d => d.PriceChange, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.LastPr) - BitgetValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.Change24h) * 100m))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => ParseTimestamp(s.Ts)));

        // BitgetSymbol -> SymbolInfo. Bitget exposes lot/tick precision under separate fields this SDK
        // does not yet surface; the numeric filter fields stay null pending a dedicated task.
        CreateMap<BitgetSymbol, SymbolInfo>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromComponents(s.BaseCoin, s.QuoteCoin)))
            .ForMember(d => d.AllowedOrderTypes, o => o.MapFrom(s => DefaultSpotOrderTypes))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TickSize, o => o.Ignore())
            .ForMember(d => d.MinQuantity, o => o.Ignore())
            .ForMember(d => d.MaxQuantity, o => o.Ignore())
            .ForMember(d => d.StepSize, o => o.Ignore())
            .ForMember(d => d.MinNotional, o => o.Ignore());

        // BitgetBalance -> AssetBalance. Bitget splits unavailable balance across frozen (open orders)
        // and locked (other holds); both count toward the domain Locked. Long-tail coins map to
        // Asset.None rather than throwing.
        CreateMap<BitgetBalance, AssetBalance>()
            .ForMember(d => d.Asset, o => o.MapFrom(s => BitgetValueParsers.ParseAssetOrNone(s.Coin)))
            .ForMember(d => d.Free, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.Available)))
            .ForMember(d => d.Locked, o => o.MapFrom(s => BitgetValueParsers.ParseDecimal(s.Frozen) + BitgetValueParsers.ParseDecimal(s.Locked)));
    }

    /// <summary>The order types Bitget V2 spot accepts (stop/take-profit use the separate plan API).</summary>
    private static readonly OrderType[] DefaultSpotOrderTypes = [OrderType.Limit, OrderType.Market];

    /// <summary>Parses a Bitget string-encoded unix-ms timestamp into an optional instant (null when unset/zero).</summary>
    private static DateTimeOffset? ParseTimestamp(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ms)
            || ms <= 0)
            return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }
}
