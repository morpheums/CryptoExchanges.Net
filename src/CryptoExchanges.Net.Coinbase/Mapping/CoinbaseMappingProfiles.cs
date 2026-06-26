using DeltaMapper;
using CryptoExchanges.Net.Coinbase.Dtos;
using CryptoExchanges.Net.Coinbase.Internal;

namespace CryptoExchanges.Net.Coinbase.Mapping;

/// <summary>
/// DeltaMapper profile mapping Coinbase Advanced Trade REST DTOs to venue-neutral domain models.
/// Decimal, enum, and timestamp fields are converted via <see cref="CoinbaseValueParsers"/> so behaviour
/// matches the hand-written projections elsewhere in the service exactly. Wire symbol strings are
/// resolved through the injected <see cref="ISymbolMapper"/>.
/// </summary>
internal sealed class CoinbaseResponseProfile : Profile
{
    /// <summary>
    /// Creates the profile, capturing the <paramref name="symbolMapper"/> used to resolve wire symbol
    /// strings into <see cref="Symbol"/> values inside the mapping expressions.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper used for wire-string resolution.</param>
    public CoinbaseResponseProfile(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);

        CreateMap<OrderDto, Order>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.ProductId)))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrderId))
            .ForMember(d => d.ClientOrderId, o => o.MapFrom(s => string.IsNullOrEmpty(s.ClientOrderId) ? null : s.ClientOrderId))
            .ForMember(d => d.Price, o => o.MapFrom(s => CoinbaseValueParsers.ParseLimitPrice(s.OrderConfiguration)))
            .ForMember(d => d.OriginalQuantity, o => o.MapFrom(s => CoinbaseValueParsers.ParseOriginalQuantity(s.OrderConfiguration)))
            .ForMember(d => d.ExecutedQuantity, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.FilledSize)))
            .ForMember(d => d.Side, o => o.MapFrom(s => CoinbaseValueParsers.ParseOrderSide(s.Side)))
            .ForMember(d => d.Type, o => o.MapFrom(s => CoinbaseValueParsers.ParseOrderType(s.OrderConfiguration)))
            .ForMember(d => d.Status, o => o.MapFrom(s => CoinbaseValueParsers.ParseOrderStatus(s.Status)))
            .ForMember(d => d.TimeInForce, o => o.MapFrom(s => CoinbaseValueParsers.ParseTimeInForce(s.OrderConfiguration)))
            .ForMember(d => d.StopPrice, o => o.Ignore())
            .ForMember(d => d.IcebergQuantity, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => CoinbaseValueParsers.ParseRfc3339ToTimestamp(s.CreatedTime)))
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            // Coinbase provides filled_value = average_fill_price * filled_size (cumulative quote quantity).
            .ForMember(d => d.CumulativeQuoteQuantity, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.FilledValue)));

        // TickerDto -> Ticker. Coinbase reports price_percent_chg_24h directly; open price is unavailable in the ticker endpoint.
        CreateMap<TickerDto, Ticker>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.ProductId)))
            .ForMember(d => d.LastPrice, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.OpenPrice, o => o.Ignore())
            .ForMember(d => d.HighPrice, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.High24h)))
            .ForMember(d => d.LowPrice, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Low24h)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Volume24h)))
            .ForMember(d => d.QuoteVolume, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Volume24hUsd)))
            .ForMember(d => d.PriceChange, o => o.Ignore())
            .ForMember(d => d.PriceChangePercent, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.PricePercentChg24h)))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => CoinbaseValueParsers.ParseRfc3339ToTimestamp(s.Time)));

        // SymbolInfoDto -> SymbolInfo. Coinbase products expose base/quote increments and min sizes.
        CreateMap<SymbolInfoDto, SymbolInfo>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromComponents(s.BaseCurrencyId, s.QuoteCurrencyId)))
            .ForMember(d => d.AllowedOrderTypes, o => o.MapFrom(s => DefaultSpotOrderTypes))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TickSize, o => o.MapFrom(s => CoinbaseValueParsers.ParseOptionalDecimal(s.QuoteIncrement)))
            .ForMember(d => d.MinQuantity, o => o.MapFrom(s => CoinbaseValueParsers.ParseOptionalDecimal(s.BaseMinSize)))
            .ForMember(d => d.MaxQuantity, o => o.MapFrom(s => CoinbaseValueParsers.ParseOptionalDecimal(s.BaseMaxSize)))
            .ForMember(d => d.StepSize, o => o.MapFrom(s => CoinbaseValueParsers.ParseOptionalDecimal(s.BaseIncrement)))
            .ForMember(d => d.MinNotional, o => o.MapFrom(s => CoinbaseValueParsers.ParseOptionalDecimal(s.QuoteMinSize)));

        // AccountDto -> AssetBalance. Coinbase accounts use nested value objects for available_balance and hold.
        // Long-tail assets map to Asset.None rather than throwing.
        CreateMap<AccountDto, AssetBalance>()
            .ForMember(d => d.Asset, o => o.MapFrom(s => CoinbaseValueParsers.ParseAssetOrNone(s.Currency)))
            .ForMember(d => d.Free, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.AvailableBalance.Value)))
            .ForMember(d => d.Locked, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Hold.Value)));

        // TradeDto -> Trade. Coinbase encodes the taker side; SELL taker means the buyer was the maker.
        CreateMap<TradeDto, Trade>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.ProductId)))
            .ForMember(d => d.Id, o => o.MapFrom(s => s.TradeId))
            .ForMember(d => d.Price, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.Quantity, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Size)))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => CoinbaseValueParsers.ParseRfc3339ToTimestamp(s.Time)))
            .ForMember(d => d.IsBuyerMaker, o => o.MapFrom(s => s.Side == "SELL"))
            .ForMember(d => d.OrderId, o => o.Ignore());

        // FillDto -> Trade. Fills are executed-trade records; same IsBuyerMaker derivation as TradeDto.
        CreateMap<FillDto, Trade>()
            .ForMember(d => d.Symbol, o => o.MapFrom(s => symbolMapper.FromWire(s.ProductId)))
            .ForMember(d => d.Id, o => o.MapFrom(s => s.TradeId))
            .ForMember(d => d.Price, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Price)))
            .ForMember(d => d.Quantity, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Size)))
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => CoinbaseValueParsers.ParseRfc3339ToTimestamp(s.TradeTime)))
            .ForMember(d => d.IsBuyerMaker, o => o.MapFrom(s => s.Side == "SELL"))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.OrderId));

        // CandlestickDto -> Candlestick. Coinbase candles use unix-second start timestamps.
        CreateMap<CandlestickDto, Candlestick>()
            .ForMember(d => d.OpenTime, o => o.MapFrom(s =>
                CoinbaseValueParsers.ParseUnixSecondsToTimestamp(s.Start) ?? DateTimeOffset.MinValue))
            .ForMember(d => d.CloseTime, o => o.Ignore())
            .ForMember(d => d.Open, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Open)))
            .ForMember(d => d.High, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.High)))
            .ForMember(d => d.Low, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Low)))
            .ForMember(d => d.Close, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Close)))
            .ForMember(d => d.Volume, o => o.MapFrom(s => CoinbaseValueParsers.ParseDecimal(s.Volume)))
            .ForMember(d => d.QuoteVolume, o => o.Ignore())
            .ForMember(d => d.TradeCount, o => o.Ignore())
            .ForMember(d => d.Interval, o => o.Ignore())
            .ForMember(d => d.TradingSymbol, o => o.Ignore());
    }

    /// <summary>The order types Coinbase Advanced Trade spot supports.</summary>
    private static readonly OrderType[] DefaultSpotOrderTypes = [OrderType.Limit, OrderType.Market];
}
