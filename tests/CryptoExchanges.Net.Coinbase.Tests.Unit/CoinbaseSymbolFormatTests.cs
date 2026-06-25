using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Coinbase;
using CryptoExchanges.Net.Coinbase.Dtos;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Coinbase.Mapping;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Coinbase.Tests.Unit;

/// <summary>
/// No-network unit tests for the Coinbase symbol format, value parsers, DTO deserialization,
/// and DeltaMapper profile (DTO → domain model).
/// </summary>
public class CoinbaseSymbolFormatTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);
    private static readonly Symbol EthUsd = new(Asset.Eth, Asset.Of("USD"));

    private static SymbolMapper BuildMapper(params Symbol[] symbols)
    {
        var mapper = new SymbolMapper(CoinbaseSymbolFormat.Instance);
        if (symbols.Length > 0)
            mapper.UpdateSymbols(symbols.Select(s => new SymbolInfo(s, [OrderType.Limit, OrderType.Market])));
        return mapper;
    }

    private static IMapper BuildDeltaMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new CoinbaseResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    [Fact]
    public void ToWire_BtcUsdt_ReturnsDashDelimited()
        => BuildMapper().ToWire(BtcUsdt).Should().Be("BTC-USDT");

    [Fact]
    public void ToWire_EthUsd_ReturnsDashDelimited()
        => BuildMapper().ToWire(EthUsd).Should().Be("ETH-USD");

    [Fact]
    public void ToWire_AlwaysUpperCase()
    {
        var mapper = BuildMapper();
        mapper.ToWire(BtcUsdt).Should().Be(mapper.ToWire(BtcUsdt).ToUpperInvariant());
    }

    [Fact]
    public void FromWire_BtcUsd_WarmCache_ResolvesSymbol()
    {
        var btcUsd = new Symbol(Asset.Btc, Asset.Of("USD"));
        var mapper = BuildMapper(btcUsd);
        mapper.FromWire("BTC-USD").Should().Be(btcUsd);
    }

    [Fact]
    public void FromWire_BtcUsdt_ColdCache_FallbackDelimiterParse_Resolves()
        => BuildMapper().FromWire("BTC-USDT").Should().Be(BtcUsdt);

    [Fact]
    public void FromWire_EthUsd_ColdCache_FallbackDelimiterParse_Resolves()
        => BuildMapper().FromWire("ETH-USD").Should().Be(EthUsd);

    [Fact]
    public void FromWire_NullOrEmpty_ThrowsArgumentException()
    {
        var act = () => BuildMapper().FromWire("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromWire_NonAlphaNumericWireString_ThrowsFormatException()
    {
        var act = () => BuildMapper(BtcUsdt).FromWire("INVALID WIRE");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ToWire_ThenFromWire_BtcUsdt_RoundTrips()
    {
        var mapper = BuildMapper(BtcUsdt);
        mapper.FromWire(mapper.ToWire(BtcUsdt)).Should().Be(BtcUsdt);
    }

    [Fact]
    public void ToWire_ThenFromWire_EthUsd_RoundTrips()
    {
        var mapper = BuildMapper(EthUsd);
        mapper.FromWire(mapper.ToWire(EthUsd)).Should().Be(EthUsd);
    }

    [Fact]
    public void FromWire_WireStringWithInvalidAssetChars_ThrowsFormatException()
    {
        // Characters like '!' are rejected by Asset.TryOf, causing cold-cache resolution to fail.
        var act = () => BuildMapper().FromWire("BTC!-USD");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseDecimal_ValidString_ReturnsParsedValue()
        => CoinbaseValueParsers.ParseDecimal("123.45").Should().Be(123.45m);

    [Fact]
    public void ParseDecimal_EmptyString_ReturnsZero()
        => CoinbaseValueParsers.ParseDecimal("").Should().Be(0m);

    [Fact]
    public void ParseDecimal_ZeroString_ReturnsZero()
        => CoinbaseValueParsers.ParseDecimal("0").Should().Be(0m);

    [Fact]
    public void ParseOptionalDecimal_ValidString_ReturnsParsedValue()
        => CoinbaseValueParsers.ParseOptionalDecimal("50.0").Should().Be(50.0m);

    [Fact]
    public void ParseOptionalDecimal_EmptyString_ReturnsNull()
        => CoinbaseValueParsers.ParseOptionalDecimal("").Should().BeNull();

    [Fact]
    public void ParseOptionalDecimal_ZeroString_ReturnsNull()
        => CoinbaseValueParsers.ParseOptionalDecimal("0").Should().BeNull();

    [Fact]
    public void ParseOrderSide_Buy_ReturnsBuy()
        => CoinbaseValueParsers.ParseOrderSide("BUY").Should().Be(OrderSide.Buy);

    [Fact]
    public void ParseOrderSide_Sell_ReturnsSell()
        => CoinbaseValueParsers.ParseOrderSide("SELL").Should().Be(OrderSide.Sell);

    [Fact]
    public void ParseOrderSide_Unknown_ThrowsArgumentOutOfRange()
    {
        var act = () => CoinbaseValueParsers.ParseOrderSide("buy");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("OPEN", OrderStatus.New)]
    [InlineData("FILLED", OrderStatus.Filled)]
    [InlineData("CANCELLED", OrderStatus.Canceled)]
    [InlineData("EXPIRED", OrderStatus.Expired)]
    [InlineData("FAILED", OrderStatus.Rejected)]
    [InlineData("UNKNOWN_ORDER_STATUS", OrderStatus.Unknown)]
    public void ParseOrderStatus_ReturnsExpected(string input, OrderStatus expected)
        => CoinbaseValueParsers.ParseOrderStatus(input).Should().Be(expected);

    [Fact]
    public void ParseRfc3339ToTimestamp_ValidInput_ReturnsDateTimeOffset()
    {
        var result = CoinbaseValueParsers.ParseRfc3339ToTimestamp("2023-11-15T00:00:00Z");
        result.Should().NotBeNull();
        result!.Value.ToUnixTimeMilliseconds().Should().Be(1700006400000L);
    }

    [Fact]
    public void ParseRfc3339ToTimestamp_EmptyString_ReturnsNull()
        => CoinbaseValueParsers.ParseRfc3339ToTimestamp("").Should().BeNull();

    [Fact]
    public void ParseUnixSecondsToTimestamp_ValidString_ReturnsDateTimeOffset()
    {
        var result = CoinbaseValueParsers.ParseUnixSecondsToTimestamp("1700000000");
        result.Should().NotBeNull();
        result!.Value.ToUnixTimeMilliseconds().Should().Be(1700000000000L);
    }

    [Fact]
    public void ParseUnixSecondsToTimestamp_ZeroString_ReturnsNull()
        => CoinbaseValueParsers.ParseUnixSecondsToTimestamp("0").Should().BeNull();

    [Fact]
    public void ParseAssetOrNone_BTC_ReturnsBtcAsset()
        => CoinbaseValueParsers.ParseAssetOrNone("BTC").Should().Be(Asset.Btc);

    [Fact]
    public void ParseAssetOrNone_Empty_ReturnsNone()
        => CoinbaseValueParsers.ParseAssetOrNone("").Should().Be(Asset.None);

    [Fact]
    public void ParseAssetOrNone_InvalidTicker_ReturnsNone()
        => CoinbaseValueParsers.ParseAssetOrNone("INVALID!@#$").Should().Be(Asset.None);

    [Fact]
    public void TickerDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "product_id": "BTC-USDT",
              "price": "42000.5",
              "price_percent_chg_24h": "2.5",
              "high_24h": "43000.0",
              "low_24h": "39500.0",
              "volume_24h": "123.45",
              "volume_24h_usd": "5000000.0",
              "time": "2023-11-14T22:13:20Z"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<TickerDto>(json);

        dto.Should().NotBeNull();
        dto!.ProductId.Should().Be("BTC-USDT");
        dto.Price.Should().Be("42000.5");
        dto.PricePercentChg24h.Should().Be("2.5");
        dto.High24h.Should().Be("43000.0");
        dto.Volume24h.Should().Be("123.45");
    }

    [Fact]
    public void OrderBookDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "product_id": "BTC-USDT",
              "bids": [{"price": "42000.0", "size": "2.0"}],
              "asks": [{"price": "42001.0", "size": "0.5"}],
              "time": "2023-11-14T22:13:20Z"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<OrderBookDto>(json);

        dto.Should().NotBeNull();
        dto!.Bids.Should().HaveCount(1);
        dto.Asks.Should().HaveCount(1);
        dto.Bids[0].Price.Should().Be("42000.0");
        dto.Asks[0].Price.Should().Be("42001.0");
    }

    [Fact]
    public void CandlestickDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "start": "1700000000",
              "open": "40000.0",
              "high": "43000.0",
              "low": "39500.0",
              "close": "42000.5",
              "volume": "123.45"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<CandlestickDto>(json);

        dto.Should().NotBeNull();
        dto!.Start.Should().Be("1700000000");
        dto.Close.Should().Be("42000.5");
    }

    [Fact]
    public void TradeDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "trade_id": "trade-1",
              "product_id": "BTC-USDT",
              "price": "42000.5",
              "size": "0.1",
              "side": "BUY",
              "time": "2023-11-14T22:13:20Z"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<TradeDto>(json);

        dto.Should().NotBeNull();
        dto!.TradeId.Should().Be("trade-1");
        dto.Side.Should().Be("BUY");
    }

    [Fact]
    public void AccountDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "uuid": "acc-123",
              "currency": "BTC",
              "available_balance": {"value": "1.5", "currency": "BTC"},
              "hold": {"value": "0.25", "currency": "BTC"}
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<AccountDto>(json);

        dto.Should().NotBeNull();
        dto!.Currency.Should().Be("BTC");
        dto.AvailableBalance.Value.Should().Be("1.5");
        dto.Hold.Value.Should().Be("0.25");
    }

    [Fact]
    public void FillDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "entry_id": "entry-1",
              "trade_id": "trade-1",
              "order_id": "ord-123",
              "product_id": "BTC-USDT",
              "price": "42000.0",
              "size": "0.1",
              "side": "BUY",
              "liquidity_indicator": "TAKER",
              "trade_time": "2023-11-14T22:13:20Z"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<FillDto>(json);

        dto.Should().NotBeNull();
        dto!.TradeId.Should().Be("trade-1");
        dto.OrderId.Should().Be("ord-123");
        dto.LiquidityIndicator.Should().Be("TAKER");
    }

    [Fact]
    public void OrderDto_Deserializes_LimitGtcOrder()
    {
        const string json = """
            {
              "order_id": "ord-111",
              "client_order_id": "cli-1",
              "product_id": "BTC-USDT",
              "side": "BUY",
              "status": "OPEN",
              "order_configuration": {
                "limit_limit_gtc": {
                  "base_size": "1.0",
                  "limit_price": "42000.0",
                  "post_only": false
                }
              },
              "filled_size": "0",
              "average_filled_price": "0",
              "filled_value": "0",
              "total_fees": "0",
              "created_time": "2023-11-14T22:13:20Z"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<OrderDto>(json);

        dto.Should().NotBeNull();
        dto!.OrderId.Should().Be("ord-111");
        dto.Status.Should().Be("OPEN");
        dto.OrderConfiguration!.LimitGtc!.LimitPrice.Should().Be("42000.0");
    }

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var act = () => BuildDeltaMapper(symbolMapper);
        act.Should().NotThrow();
    }

    [Fact]
    public void TickerProfile_MapsAllScalarsAndResolvesSymbol()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new TickerDto
        {
            ProductId = "BTC-USDT",
            Price = "42000",
            PricePercentChg24h = "5.0",
            High24h = "43000",
            Low24h = "39000",
            Volume24h = "123.45",
            Volume24hUsd = "5000000",
            Time = "2023-11-14T22:13:20Z"
        };

        var ticker = mapper.Map<TickerDto, Ticker>(dto);

        ticker.Symbol.Should().Be(BtcUsdt);
        ticker.LastPrice.Should().Be(42000m);
        ticker.HighPrice.Should().Be(43000m);
        ticker.LowPrice.Should().Be(39000m);
        ticker.Volume.Should().Be(123.45m);
        ticker.QuoteVolume.Should().Be(5000000m);
        ticker.PriceChangePercent.Should().Be(5.0m);
        ticker.Timestamp.Should().NotBeNull();
    }

    [Fact]
    public void AccountProfile_MapsAvailableAndHold()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new AccountDto
        {
            Uuid = "acc-1",
            Currency = "BTC",
            AvailableBalance = new AmountDto { Value = "1.5", Currency = "BTC" },
            Hold = new AmountDto { Value = "0.25", Currency = "BTC" }
        };

        var balance = mapper.Map<AccountDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(1.5m);
        balance.Locked.Should().Be(0.25m);
        balance.Total.Should().Be(1.75m);
    }

    [Fact]
    public void AccountProfile_InvalidCurrency_MapsToAssetNone()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new AccountDto
        {
            Currency = "INVALID!@#",
            AvailableBalance = new AmountDto { Value = "10", Currency = "INVALID!@#" },
            Hold = new AmountDto { Value = "0", Currency = "INVALID!@#" }
        };

        var balance = mapper.Map<AccountDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.None);
        balance.Free.Should().Be(10m);
    }

    [Fact]
    public void OrderProfile_LimitGtcOrder_MapsAllScalars()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new OrderDto
        {
            OrderId = "ord-111",
            ClientOrderId = "cli-1",
            ProductId = "BTC-USDT",
            Side = "BUY",
            Status = "OPEN",
            OrderConfiguration = new OrderConfigurationDto
            {
                LimitGtc = new LimitGtcDto { BaseSize = "1.0", LimitPrice = "42000.0" }
            },
            FilledSize = "0.5",
            FilledValue = "21000",
            CreatedTime = "2023-11-14T22:13:20Z"
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.Symbol.Should().Be(BtcUsdt);
        order.OrderId.Should().Be("ord-111");
        order.ClientOrderId.Should().Be("cli-1");
        order.Price.Should().Be(42000m);
        order.OriginalQuantity.Should().Be(1.0m);
        order.ExecutedQuantity.Should().Be(0.5m);
        order.CumulativeQuoteQuantity.Should().Be(21000m);
        order.Side.Should().Be(OrderSide.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.Status.Should().Be(OrderStatus.New);
        order.TimeInForce.Should().Be(TimeInForce.Gtc);
        order.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public void OrderProfile_MarketIocOrder_MapsTypeAndTif()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new OrderDto
        {
            OrderId = "ord-222",
            ProductId = "BTC-USDT",
            Side = "SELL",
            Status = "FILLED",
            OrderConfiguration = new OrderConfigurationDto
            {
                MarketIoc = new MarketIocDto { QuoteSize = "1000", BaseSize = "0" }
            },
            FilledSize = "0.02380",
            FilledValue = "1000",
            CreatedTime = "2023-11-14T22:13:20Z"
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.Side.Should().Be(OrderSide.Sell);
        order.Type.Should().Be(OrderType.Market);
        order.Status.Should().Be(OrderStatus.Filled);
        order.TimeInForce.Should().Be(TimeInForce.Ioc);
    }

    [Fact]
    public void OrderProfile_NoClientOrderId_MapsNull()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new OrderDto
        {
            OrderId = "ord-333",
            ClientOrderId = "",
            ProductId = "BTC-USDT",
            Side = "BUY",
            Status = "OPEN",
            OrderConfiguration = new OrderConfigurationDto
            {
                LimitGtc = new LimitGtcDto { BaseSize = "1.0", LimitPrice = "42000.0" }
            },
            CreatedTime = "2023-11-14T22:13:20Z"
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.ClientOrderId.Should().BeNull();
    }

    [Fact]
    public void TradeProfile_BuySide_IsBuyerMakerFalse()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new TradeDto
        {
            TradeId = "t-1",
            ProductId = "BTC-USDT",
            Price = "42000",
            Size = "0.1",
            Side = "BUY",
            Time = "2023-11-14T22:13:20Z"
        };

        var trade = mapper.Map<TradeDto, Trade>(dto);

        trade.Symbol.Should().Be(BtcUsdt);
        trade.Price.Should().Be(42000m);
        // BUY taker means seller was the maker; IsBuyerMaker = false
        trade.IsBuyerMaker.Should().BeFalse();
    }

    [Fact]
    public void TradeProfile_SellSide_IsBuyerMakerTrue()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new TradeDto
        {
            TradeId = "t-2",
            ProductId = "BTC-USDT",
            Price = "42000",
            Size = "0.1",
            Side = "SELL",
            Time = "2023-11-14T22:13:20Z"
        };

        var trade = mapper.Map<TradeDto, Trade>(dto);

        // SELL taker means buyer was the maker; IsBuyerMaker = true
        trade.IsBuyerMaker.Should().BeTrue();
    }

    [Fact]
    public void CandlestickProfile_MapsOpenTimeFromUnixSeconds()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new CandlestickDto
        {
            Start = "1700000000",
            Open = "40000",
            High = "43000",
            Low = "39000",
            Close = "42000",
            Volume = "123.45"
        };

        var candle = mapper.Map<CandlestickDto, Candlestick>(dto);

        candle.OpenTime.ToUnixTimeMilliseconds().Should().Be(1700000000000L);
        candle.Open.Should().Be(40000m);
        candle.High.Should().Be(43000m);
        candle.Close.Should().Be(42000m);
        candle.Volume.Should().Be(123.45m);
    }

    [Fact]
    public void FillProfile_MapsToTradeWithOrderId()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new FillDto
        {
            EntryId = "entry-1",
            TradeId = "t-1",
            OrderId = "ord-1",
            ProductId = "BTC-USDT",
            Price = "42000",
            Size = "0.1",
            Side = "BUY",
            LiquidityIndicator = "TAKER",
            TradeTime = "2023-11-14T22:13:20Z"
        };

        var trade = mapper.Map<FillDto, Trade>(dto);

        trade.Id.Should().Be("t-1");
        trade.OrderId.Should().Be("ord-1");
        trade.Price.Should().Be(42000m);
        trade.IsBuyerMaker.Should().BeFalse();
    }

    [Fact]
    public void SymbolInfoProfile_MapsIncrements()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new SymbolInfoDto
        {
            ProductId = "BTC-USDT",
            BaseCurrencyId = "BTC",
            QuoteCurrencyId = "USDT",
            BaseIncrement = "0.00000001",
            QuoteIncrement = "0.01",
            BaseMinSize = "0.001",
            BaseMaxSize = "10000",
            QuoteMinSize = "1"
        };

        var info = mapper.Map<SymbolInfoDto, SymbolInfo>(dto);

        info.Symbol.Should().Be(BtcUsdt);
        info.AllowedOrderTypes.Should().Contain(OrderType.Limit).And.Contain(OrderType.Market);
        info.StepSize.Should().Be(0.00000001m);
        info.TickSize.Should().Be(0.01m);
        info.MinQuantity.Should().Be(0.001m);
        info.MinNotional.Should().Be(1m);
    }
}
