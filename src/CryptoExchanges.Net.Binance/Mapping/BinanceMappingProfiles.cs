using DeltaMapper;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Binance.Services;

namespace CryptoExchanges.Net.Binance.Mapping;

/// <summary>
/// DeltaMapper profile mapping Binance response DTOs to venue-neutral domain models.
/// Decimal, enum, and timestamp fields are converted via <see cref="BinanceValueParsers"/>
/// so behavior matches the original hand-written mappings exactly. Wire symbol strings are
/// resolved through the injected <see cref="ISymbolMapper"/>.
/// </summary>
internal sealed class BinanceResponseProfile : Profile
{
    /// <summary>
    /// Creates the profile, capturing the <paramref name="symbolMapper"/> used to resolve
    /// wire symbol strings into <see cref="Symbol"/> values inside the mapping expressions.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper used for wire-string resolution.</param>
    public BinanceResponseProfile(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);

        CreateMap<OrderResponseDto, Order>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrderId.ToString()))
            .ForMember(d => d.ClientOrderId, o => o.MapFrom(s => string.IsNullOrEmpty(s.ClientOrderId) ? null : s.ClientOrderId))
            .ForMember(d => d.Price, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.OriginalQuantity, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.OrigQty)))
            .ForMember(d => d.ExecutedQuantity, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.ExecutedQty)))
            .ForMember(d => d.Side, o => o.MapFrom(s => BinanceValueParsers.ParseOrderSide(s.Side)))
            .ForMember(d => d.Type, o => o.MapFrom(s => BinanceValueParsers.ParseOrderType(s.Type)))
            .ForMember(d => d.Status, o => o.MapFrom(s => BinanceValueParsers.ParseOrderStatus(s.Status)))
            .ForMember(d => d.TimeInForce, o => o.MapFrom(s => BinanceValueParsers.ParseTimeInForce(s.TimeInForce)))
            .ForMember(d => d.StopPrice, o => o.MapFrom(s => BinanceValueParsers.ParseOptionalDecimal(s.StopPrice)))
            .ForMember(d => d.IcebergQuantity, o => o.MapFrom(s => BinanceValueParsers.ParseOptionalDecimal(s.IcebergQty)))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.Time > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(s.Time) : (DateTimeOffset?)null))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(s => s.UpdateTime > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(s.UpdateTime) : (DateTimeOffset?)null))
            .ForMember(d => d.CumulativeQuoteQuantity, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.CumulativeQuoteQty)));

        CreateMap<TickerResponseDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.Symbol)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.LastPrice)))
            .ForMember(d => d.OpenPrice, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.OpenPrice)))
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.HighPrice)))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.LowPrice)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.Volume)))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.QuoteVolume)))
            .ForMember(d => d.PriceChange, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.PriceChange)))
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.PriceChangePercent)))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => s.CloseTime > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(s.CloseTime) : (DateTimeOffset?)null));

        // NOTE: TradeHistoryResponseDto -> Trade is intentionally NOT mapped here.
        // Trade.Symbol for account trade history comes from the caller's typed Symbol argument
        // (which the caller already holds), not from resolving the wire string — resolving via
        // FromWire could throw on a cold mapper cache for pairs outside the fallback-quote list.
        // That projection stays hand-written in BinanceAccountService.GetTradeHistoryAsync.

        // SymbolInfoDto -> SymbolInfo (exchangeInfo symbol projection; explicit base/quote legs).
        CreateMap<SymbolInfoDto, SymbolInfo>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromComponents(s.BaseAsset, s.QuoteAsset)))
            .ForMember(d => d.AllowedOrderTypes, o => o.MapFrom(s => s.OrderTypes.Select(BinanceValueParsers.ParseOrderType).ToArray()))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TickSize, o => o.Ignore())
            .ForMember(d => d.MinQuantity, o => o.Ignore())
            .ForMember(d => d.MaxQuantity, o => o.Ignore())
            .ForMember(d => d.StepSize, o => o.Ignore())
            .ForMember(d => d.MinNotional, o => o.Ignore());

        // Balances are where long-tail assets appear, so an unrepresentable ticker maps to
        // Asset.None rather than throwing.
        CreateMap<BalanceDto, AssetBalance>()
            .ForMember(d => d.Asset, o => o.MapFrom(s => BinanceValueParsers.ParseAssetOrNone(s.Asset)))
            .ForMember(d => d.Free, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.Free)))
            .ForMember(d => d.Locked, o => o.MapFrom(s => BinanceValueParsers.ParseDecimal(s.Locked)));
    }
}
