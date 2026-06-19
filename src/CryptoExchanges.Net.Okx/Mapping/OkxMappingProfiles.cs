using DeltaMapper;
using CryptoExchanges.Net.Okx.Internal;
using CryptoExchanges.Net.Okx.Services;

namespace CryptoExchanges.Net.Okx.Mapping;

/// <summary>
/// DeltaMapper profile mapping OKX V5 response DTOs to venue-neutral domain models.
/// Decimal, enum, and timestamp fields are converted via <see cref="OkxValueParsers"/> so behaviour
/// matches the hand-written projections elsewhere in the service exactly. Wire symbol strings are
/// resolved through the injected <see cref="ISymbolMapper"/>.
/// </summary>
internal sealed class OkxResponseProfile : Profile
{
    /// <summary>
    /// Creates the profile, capturing the <paramref name="symbolMapper"/> used to resolve wire symbol
    /// strings into <see cref="Symbol"/> values inside the mapping expressions.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper used for wire-string resolution.</param>
    public OkxResponseProfile(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);

        CreateMap<OrderDto, Order>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.InstId)))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrdId))
            .ForMember(d => d.ClientOrderId, o => o.MapFrom(s => string.IsNullOrEmpty(s.ClOrdId) ? null : s.ClOrdId))
            .ForMember(d => d.Price, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.Px)))
            .ForMember(d => d.OriginalQuantity, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.Sz)))
            .ForMember(d => d.ExecutedQuantity, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.AccFillSz)))
            .ForMember(d => d.Side, o => o.MapFrom(s => OkxValueParsers.ParseOrderSide(s.Side)))
            .ForMember(d => d.Type, o => o.MapFrom(s => OkxValueParsers.ParseOrderType(s.OrdType)))
            .ForMember(d => d.Status, o => o.MapFrom(s => OkxValueParsers.ParseOrderStatus(s.State)))
            .ForMember(d => d.TimeInForce, o => o.MapFrom(s => OkxValueParsers.ParseTimeInForce(s.OrdType)))
            .ForMember(d => d.StopPrice, o => o.Ignore())
            .ForMember(d => d.IcebergQuantity, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => ParseTimestamp(s.CTime)))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(s => ParseTimestamp(s.UTime)))
            // OKX gives accumulated base size (accFillSz) + average fill price (avgPx); the cumulative
            // quote quantity is their product (0 when there are no fills, since avgPx parses to 0).
            .ForMember(d => d.CumulativeQuoteQuantity, o => o.MapFrom(s =>
                OkxValueParsers.ParseDecimal(s.AccFillSz) * OkxValueParsers.ParseDecimal(s.AvgPx)));

        // TickerDto -> Ticker. OKX reports open24h (the price 24h ago); price change is last - open24h,
        // and the percent is that change over open24h * 100 (OKX exposes no fractional-change field).
        CreateMap<TickerDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.InstId)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.Last)))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.Open24h)))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.High24h)))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.Low24h)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.Vol24h)))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.VolCcy24h)))
            .ForMember(d => d.PriceChange, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.Last) - OkxValueParsers.ParseDecimal(s.Open24h)))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s =>
                OkxValueParsers.ParseDecimal(s.Open24h) == 0m
                    ? 0m
                    : (OkxValueParsers.ParseDecimal(s.Last) - OkxValueParsers.ParseDecimal(s.Open24h)) / OkxValueParsers.ParseDecimal(s.Open24h) * 100m))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => ParseTimestamp(s.Ts)));

        // SymbolInfoDto -> SymbolInfo. OKX instruments expose lot/tick filters under separate fields
        // this SDK does not yet surface; the numeric filter fields stay null pending a dedicated task.
        CreateMap<SymbolInfoDto, SymbolInfo>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromComponents(s.BaseCcy, s.QuoteCcy)))
            .ForMember(d => d.AllowedOrderTypes, o => o.MapFrom(s => DefaultSpotOrderTypes))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TickSize, o => o.Ignore())
            .ForMember(d => d.MinQuantity, o => o.Ignore())
            .ForMember(d => d.MaxQuantity, o => o.Ignore())
            .ForMember(d => d.StepSize, o => o.Ignore())
            .ForMember(d => d.MinNotional, o => o.Ignore());

        // BalanceDto -> AssetBalance. OKX reports availBal (free) + frozenBal (locked) directly.
        // Long-tail coins map to Asset.None rather than throwing.
        CreateMap<BalanceDto, AssetBalance>()
            .ForMember(d => d.Asset, o => o.MapFrom(s => OkxValueParsers.ParseAssetOrNone(s.Ccy)))
            .ForMember(d => d.Free, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.AvailBal)))
            .ForMember(d => d.Locked, o => o.MapFrom(s => OkxValueParsers.ParseDecimal(s.FrozenBal)));
    }

    /// <summary>The order types OKX V5 spot accepts (stop/take-profit use the separate algo API).</summary>
    private static readonly OrderType[] DefaultSpotOrderTypes = [OrderType.Limit, OrderType.Market];

    /// <summary>Parses an OKX string-encoded unix-ms timestamp into an optional instant (null when unset/zero).</summary>
    private static DateTimeOffset? ParseTimestamp(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ms)
            || ms <= 0)
            return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }
}
