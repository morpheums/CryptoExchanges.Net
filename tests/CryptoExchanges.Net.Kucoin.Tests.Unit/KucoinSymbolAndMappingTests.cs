using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Internal;
using CryptoExchanges.Net.Kucoin.Mapping;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit;

/// <summary>
/// No-network unit tests for:
/// <list type="bullet">
///   <item>KuCoin bespoke <see cref="KucoinSymbolMapper"/> symbol round-trips and <c>IsSupported</c>.</item>
///   <item><see cref="KucoinValueParsers"/> decimal, enum, and timestamp helpers.</item>
///   <item>DTO JSON deserialization round-trips (representative KuCoin payloads).</item>
///   <item><see cref="KucoinResponseProfile"/> DeltaMapper DTO→model assertions.</item>
/// </list>
/// All tests are network-free.
/// </summary>
public class KucoinSymbolAndMappingTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);
    private static readonly Symbol EthUsdt = new(Asset.Eth, Asset.Usdt);

    // ── Symbol mapper helpers ──

    private static KucoinSymbolMapper BuildMapper(params Symbol[] symbols)
    {
        var mapper = new KucoinSymbolMapper();
        if (symbols.Length > 0)
            mapper.UpdateSymbols(symbols.Select(s => new SymbolInfo(s, [OrderType.Limit, OrderType.Market])));
        return mapper;
    }

    private static IMapper BuildDeltaMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new KucoinResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    // ─────────────────────────────────────────────────────────────────────
    // ── Symbol mapping ──
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToWire_BtcUsdt_ReturnsDashDelimited()
    {
        var mapper = BuildMapper();
        mapper.ToWire(BtcUsdt).Should().Be("BTC-USDT");
    }

    [Fact]
    public void ToWire_EthUsdt_ReturnsDashDelimited()
    {
        var mapper = BuildMapper();
        mapper.ToWire(EthUsdt).Should().Be("ETH-USDT");
    }

    [Fact]
    public void ToWire_AlwaysUpperCase()
    {
        var mapper = BuildMapper();
        // KuCoin format is always upper-case
        mapper.ToWire(BtcUsdt).Should().Be(mapper.ToWire(BtcUsdt).ToUpperInvariant());
    }

    [Fact]
    public void FromWire_WarmCache_ResolvesSymbol()
    {
        var mapper = BuildMapper(BtcUsdt);
        var result = mapper.FromWire("BTC-USDT");
        result.Should().Be(BtcUsdt);
    }

    [Fact]
    public void FromWire_ColdCache_FallbackDelimiterParse_Resolves()
    {
        // Cold cache: the delimiter-based fallback in SymbolMapper resolves BTC-USDT.
        var mapper = BuildMapper();
        var result = mapper.FromWire("BTC-USDT");
        result.Should().Be(BtcUsdt);
    }

    [Fact]
    public void FromWire_NonAlphaNumericWireString_ThrowsFormatException()
    {
        var mapper = BuildMapper(BtcUsdt);
        // A wire string with invalid asset characters (e.g. spaces) cannot be resolved.
        // FromWire propagates FormatException directly per ISymbolMapper contract and sibling
        // exchange pattern (OKX/Binance/Bybit do not re-wrap).
        var act = () => mapper.FromWire("INVALID WIRE");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void FromWire_NullOrEmpty_ThrowsArgumentException()
    {
        var mapper = BuildMapper();
        var act = () => mapper.FromWire("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromComponents_BuildsSymbolFromBaseQuote()
    {
        var mapper = BuildMapper();
        var result = mapper.FromComponents("BTC", "USDT");
        result.Should().Be(BtcUsdt);
    }

    [Fact]
    public void FromComponents_EmptyBase_ThrowsArgumentException()
    {
        var mapper = BuildMapper();
        var act = () => mapper.FromComponents("", "USDT");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromComponents_EmptyQuote_ThrowsArgumentException()
    {
        var mapper = BuildMapper();
        var act = () => mapper.FromComponents("BTC", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsSupported_RegisteredSymbol_ReturnsTrue()
    {
        var mapper = BuildMapper(BtcUsdt);
        mapper.IsSupported(BtcUsdt).Should().BeTrue();
    }

    [Fact]
    public void IsSupported_ParseableSymbol_NotInTable_ReturnsTrue()
    {
        // ETH-USDT is not registered but the delimiter-based cold-cache fallback parses it.
        // IsSupported returns true for any parseable symbol on the cold path.
        var mapper = BuildMapper(BtcUsdt);
        mapper.IsSupported(EthUsdt).Should().BeTrue();
    }

    [Fact]
    public void IsSupported_UnresolvableSymbol_ReturnsFalse()
    {
        // A Symbol whose asset tickers have invalid characters cannot be converted to a wire string
        // that round-trips back, so IsSupported returns false.
        var mapper = BuildMapper();
        // Use a symbol with a currency that forms a wire string the inner mapper cannot resolve
        // from its cold cache (empty asset tickers produce an empty wire string → FormatException).
        // default(Symbol) has zero-valued base/quote — ToWire produces an empty or null wire string
        // that FromWire cannot parse.
        var act = () => mapper.IsSupported(default);
        // IsSupported must return false, not throw.
        act.Should().NotThrow();
        mapper.IsSupported(default).Should().BeFalse();
    }

    [Fact]
    public void IsSupported_RegisteredSymbol_ReturnsTrue_ConfirmExistingBehavior()
    {
        // Renamed from the misnamed test that asserted true but was called _ReturnsFalse.
        var mapper = BuildMapper(BtcUsdt);
        mapper.IsSupported(BtcUsdt).Should().BeTrue();
    }

    [Fact]
    public void UpdateSymbols_Null_ThrowsArgumentNullException()
    {
        var mapper = BuildMapper();
        var act = () => mapper.UpdateSymbols(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ─────────────────────────────────────────────────────────────────────
    // ── KucoinValueParsers ──
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDecimal_ValidString_ReturnsParsedValue()
        => KucoinValueParsers.ParseDecimal("123.45").Should().Be(123.45m);

    [Fact]
    public void ParseDecimal_EmptyString_ReturnsZero()
        => KucoinValueParsers.ParseDecimal("").Should().Be(0m);

    [Fact]
    public void ParseDecimal_NullString_ReturnsZero()
        => KucoinValueParsers.ParseDecimal(null!).Should().Be(0m);

    [Fact]
    public void ParseDecimal_ZeroString_ReturnsZero()
        => KucoinValueParsers.ParseDecimal("0").Should().Be(0m);

    [Fact]
    public void ParseOptionalDecimal_ValidString_ReturnsParsedValue()
        => KucoinValueParsers.ParseOptionalDecimal("50.0").Should().Be(50.0m);

    [Fact]
    public void ParseOptionalDecimal_EmptyString_ReturnsNull()
        => KucoinValueParsers.ParseOptionalDecimal("").Should().BeNull();

    [Fact]
    public void ParseOptionalDecimal_ZeroString_ReturnsNull()
        => KucoinValueParsers.ParseOptionalDecimal("0").Should().BeNull();

    [Fact]
    public void ParseOrderSide_Buy_ReturnsBuy()
        => KucoinValueParsers.ParseOrderSide("buy").Should().Be(OrderSide.Buy);

    [Fact]
    public void ParseOrderSide_Sell_ReturnsSell()
        => KucoinValueParsers.ParseOrderSide("sell").Should().Be(OrderSide.Sell);

    [Fact]
    public void ParseOrderSide_Unknown_ThrowsArgumentOutOfRange()
    {
        var act = () => KucoinValueParsers.ParseOrderSide("unknown");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseOrderType_Limit_ReturnsLimit()
        => KucoinValueParsers.ParseOrderType("limit").Should().Be(OrderType.Limit);

    [Fact]
    public void ParseOrderType_Market_ReturnsMarket()
        => KucoinValueParsers.ParseOrderType("market").Should().Be(OrderType.Market);

    [Fact]
    public void ParseOrderType_Unknown_ThrowsArgumentOutOfRange()
    {
        var act = () => KucoinValueParsers.ParseOrderType("stop");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(true, false, OrderStatus.New)]
    [InlineData(false, true, OrderStatus.Canceled)]
    [InlineData(false, false, OrderStatus.Filled)]
    public void ParseOrderStatus_ReturnsExpected(bool isActive, bool cancelExist, OrderStatus expected)
        => KucoinValueParsers.ParseOrderStatus(isActive, cancelExist).Should().Be(expected);

    [Theory]
    [InlineData("GTC", TimeInForce.Gtc)]
    [InlineData("GTT", TimeInForce.Gtc)]
    [InlineData("IOC", TimeInForce.Ioc)]
    [InlineData("FOK", TimeInForce.Fok)]
    public void ParseTimeInForce_ReturnsExpected(string input, TimeInForce expected)
        => KucoinValueParsers.ParseTimeInForce(input).Should().Be(expected);

    [Fact]
    public void ParseTimeInForce_Unknown_ThrowsArgumentOutOfRange()
    {
        var act = () => KucoinValueParsers.ParseTimeInForce("DAY");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseMs_ValidString_ReturnsParsedMs()
        => KucoinValueParsers.ParseMs("1700000000000").Should().Be(1700000000000L);

    [Fact]
    public void ParseMs_EmptyString_ReturnsZero()
        => KucoinValueParsers.ParseMs("").Should().Be(0L);

    [Fact]
    public void ParseMs_NullString_ReturnsZero()
        => KucoinValueParsers.ParseMs(null!).Should().Be(0L);

    [Fact]
    public void ParseNsToMs_ValidNanoTimestamp_ConvertsToMs()
        => KucoinValueParsers.ParseNsToMs("1700000000000000000").Should().Be(1700000000000L);

    [Fact]
    public void ParseNsToMs_EmptyString_ReturnsZero()
        => KucoinValueParsers.ParseNsToMs("").Should().Be(0L);

    [Fact]
    public void ParseAssetOrNone_BTC_ReturnsBtcAsset()
        => KucoinValueParsers.ParseAssetOrNone("BTC").Should().Be(Asset.Btc);

    [Fact]
    public void ParseAssetOrNone_Empty_ReturnsNone()
        => KucoinValueParsers.ParseAssetOrNone("").Should().Be(Asset.None);

    [Fact]
    public void ParseAssetOrNone_InvalidTicker_ReturnsNone()
        // Asset is open; only tickers with invalid characters or > 32 chars map to None.
        => KucoinValueParsers.ParseAssetOrNone("INVALID!@#$").Should().Be(Asset.None);

    // ─────────────────────────────────────────────────────────────────────
    // ── DTO JSON deserialization round-trips ──
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TickerDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "symbol": "BTC-USDT",
              "last": "42000.5",
              "open": "40000.0",
              "high": "43000.0",
              "low": "39500.0",
              "vol": "123.45",
              "volValue": "5000000.0",
              "time": "1700000000000"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<TickerDto>(json);

        dto.Should().NotBeNull();
        dto!.Symbol.Should().Be("BTC-USDT");
        dto.Last.Should().Be("42000.5");
        dto.Open.Should().Be("40000.0");
        dto.High.Should().Be("43000.0");
        dto.Low.Should().Be("39500.0");
        dto.Vol.Should().Be("123.45");
        dto.VolValue.Should().Be("5000000.0");
        dto.Time.Should().Be("1700000000000");
    }

    [Fact]
    public void OrderBookDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "asks": [["42001.0", "0.5"], ["42002.0", "1.0"]],
              "bids": [["42000.0", "2.0"], ["41999.0", "1.5"]],
              "time": 1700000000000
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<OrderBookDto>(json);

        dto.Should().NotBeNull();
        dto!.Asks.Should().HaveCount(2);
        dto.Bids.Should().HaveCount(2);
        dto.Asks[0][0].Should().Be("42001.0");
        dto.Bids[0][0].Should().Be("42000.0");
        dto.Time.Should().Be(1700000000000L);
    }

    [Fact]
    public void OrderDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "id": "ord-123",
              "clientOid": "cli-1",
              "symbol": "BTC-USDT",
              "price": "42000.0",
              "size": "0.5",
              "dealSize": "0.25",
              "dealFunds": "10500.0",
              "side": "buy",
              "type": "limit",
              "isActive": true,
              "cancelExist": false,
              "timeInForce": "GTC",
              "createdAt": 1700000000000
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<OrderDto>(json);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be("ord-123");
        dto.ClientOid.Should().Be("cli-1");
        dto.Symbol.Should().Be("BTC-USDT");
        dto.Price.Should().Be("42000.0");
        dto.Side.Should().Be("buy");
        dto.Type.Should().Be("limit");
        dto.IsActive.Should().BeTrue();
        dto.TimeInForce.Should().Be("GTC");
        dto.CreatedAt.Should().Be(1700000000000L);
    }

    [Fact]
    public void FillDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "tradeId": "trade-1",
              "orderId": "ord-123",
              "symbol": "BTC-USDT",
              "price": "42000.0",
              "size": "0.1",
              "side": "buy",
              "liquidity": "taker",
              "createdAt": 1700000000000
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<FillDto>(json);

        dto.Should().NotBeNull();
        dto!.TradeId.Should().Be("trade-1");
        dto.Symbol.Should().Be("BTC-USDT");
        dto.Price.Should().Be("42000.0");
        dto.Liquidity.Should().Be("taker");
    }

    [Fact]
    public void BalanceDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "currency": "BTC",
              "available": "1.5",
              "holds": "0.25"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<BalanceDto>(json);

        dto.Should().NotBeNull();
        dto!.Currency.Should().Be("BTC");
        dto.Available.Should().Be("1.5");
        dto.Holds.Should().Be("0.25");
    }

    [Fact]
    public void ResponseDto_Deserializes_WithTickerPayload()
    {
        const string json = """
            {
              "code": "200000",
              "msg": "",
              "data": {
                "symbol": "BTC-USDT",
                "last": "42000.0",
                "open": "40000.0",
                "high": "43000.0",
                "low": "39000.0",
                "vol": "100.0",
                "volValue": "4200000.0",
                "time": "1700000000000"
              }
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<ResponseDto<TickerDto>>(json);

        dto.Should().NotBeNull();
        dto!.Code.Should().Be("200000");
        dto.Data.Should().NotBeNull();
        dto.Data!.Symbol.Should().Be("BTC-USDT");
        dto.Data.Last.Should().Be("42000.0");
    }

    [Fact]
    public void ListDto_Deserializes_WithFillItems()
    {
        const string json = """
            {
              "items": [
                {
                  "tradeId": "t1",
                  "orderId": "o1",
                  "symbol": "BTC-USDT",
                  "price": "42000.0",
                  "size": "0.1",
                  "side": "buy",
                  "liquidity": "maker",
                  "createdAt": 1700000000000
                }
              ],
              "totalNum": 1,
              "currentPage": 1,
              "pageSize": 50
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<ListDto<FillDto>>(json);

        dto.Should().NotBeNull();
        dto!.Items.Should().HaveCount(1);
        dto.Items[0].TradeId.Should().Be("t1");
        dto.TotalNum.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────
    // ── DeltaMapper profile mapping ──
    // ─────────────────────────────────────────────────────────────────────

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
            Symbol = "BTC-USDT",
            Last = "42000",
            Open = "40000",
            High = "43000",
            Low = "39000",
            Vol = "123.45",
            VolValue = "5000000",
            Time = "1700000000000"
        };

        var ticker = mapper.Map<TickerDto, Ticker>(dto);

        ticker.Symbol.Should().Be(BtcUsdt);
        ticker.LastPrice.Should().Be(42000m);
        ticker.OpenPrice.Should().Be(40000m);
        ticker.HighPrice.Should().Be(43000m);
        ticker.LowPrice.Should().Be(39000m);
        ticker.Volume.Should().Be(123.45m);
        ticker.QuoteVolume.Should().Be(5000000m);
        // PriceChange = 42000 - 40000 = 2000; PriceChangePercent = 2000/40000 * 100 = 5%
        ticker.PriceChange.Should().Be(2000m);
        ticker.PriceChangePercent.Should().Be(5m);
        ticker.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public void TickerProfile_ZeroOpen_PriceChangePercentIsZero()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new TickerDto { Symbol = "BTC-USDT", Last = "42000", Open = "0", Time = "1700000000000" };

        var ticker = mapper.Map<TickerDto, Ticker>(dto);

        ticker.PriceChangePercent.Should().Be(0m);
    }

    [Fact]
    public void OrderProfile_LimitOrder_MapsAllScalars()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new OrderDto
        {
            Id = "ord-111",
            ClientOid = "cli-1",
            Symbol = "BTC-USDT",
            Price = "42000",
            Size = "1.0",
            DealSize = "0.5",
            DealFunds = "21000",
            Side = "buy",
            Type = "limit",
            IsActive = true,
            CancelExist = false,
            TimeInForce = "GTC",
            CreatedAt = 1700000000000L
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
        order.CreatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public void OrderProfile_CancelledOrder_MapsStatusCanceled()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new OrderDto
        {
            Id = "ord-222",
            Symbol = "BTC-USDT",
            Side = "sell",
            Type = "limit",
            IsActive = false,
            CancelExist = true,
            TimeInForce = "GTC",
            CreatedAt = 1700000000000L
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public void OrderProfile_FilledOrder_MapsStatusFilled()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new OrderDto
        {
            Id = "ord-333",
            Symbol = "BTC-USDT",
            Side = "buy",
            Type = "market",
            IsActive = false,
            CancelExist = false,
            TimeInForce = "IOC",
            CreatedAt = 1700000000000L
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.Status.Should().Be(OrderStatus.Filled);
        order.Type.Should().Be(OrderType.Market);
        order.TimeInForce.Should().Be(TimeInForce.Ioc);
    }

    [Fact]
    public void OrderProfile_NoClientOid_MapsClientOrderIdNull()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new OrderDto
        {
            Id = "ord-444",
            ClientOid = "",
            Symbol = "BTC-USDT",
            Side = "buy",
            Type = "limit",
            TimeInForce = "GTC",
            CreatedAt = 1700000000000L
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.ClientOrderId.Should().BeNull();
    }

    [Fact]
    public void SymbolInfoProfile_MapsBaseQuoteToSymbol()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new SymbolInfoDto { Symbol = "BTC-USDT", BaseCurrency = "BTC", QuoteCurrency = "USDT" };

        var info = mapper.Map<SymbolInfoDto, SymbolInfo>(dto);

        info.Symbol.Should().Be(BtcUsdt);
        info.AllowedOrderTypes.Should().Contain(OrderType.Limit).And.Contain(OrderType.Market);
    }

    [Fact]
    public void BalanceProfile_MapsAvailableAndHolds()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new BalanceDto { Currency = "BTC", Available = "1.5", Holds = "0.25" };

        var balance = mapper.Map<BalanceDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(1.5m);
        balance.Locked.Should().Be(0.25m);
        balance.Total.Should().Be(1.75m);
    }

    [Fact]
    public void BalanceProfile_InvalidCurrencyChars_MapsToAssetNone()
    {
        // Asset.TryOf returns false only for empty, non-alphanumeric, or >32 char tickers.
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new BalanceDto { Currency = "INVALID!@#", Available = "10", Holds = "0" };

        var balance = mapper.Map<BalanceDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.None);
        balance.Free.Should().Be(10m);
    }

    [Fact]
    public void FillProfile_MapsToTrade()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new FillDto
        {
            TradeId = "trade-1",
            OrderId = "ord-1",
            Symbol = "BTC-USDT",
            Price = "42000.0",
            Size = "0.1",
            Side = "buy",
            Liquidity = "taker",
            CreatedAt = 1700000000000L
        };

        var trade = mapper.Map<FillDto, Trade>(dto);

        trade.Symbol.Should().Be(BtcUsdt);
        trade.Id.Should().Be("trade-1");
        trade.Price.Should().Be(42000m);
        trade.Quantity.Should().Be(0.1m);
        trade.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        // buy + taker: IsBuyerMaker = false (taker is NOT maker)
        trade.IsBuyerMaker.Should().BeFalse();
        trade.OrderId.Should().Be("ord-1");
    }

    [Fact]
    public void FillProfile_BuyMaker_IsBuyerMakerTrue()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new FillDto
        {
            TradeId = "trade-2",
            OrderId = "ord-2",
            Symbol = "BTC-USDT",
            Price = "42000.0",
            Size = "0.1",
            Side = "buy",
            Liquidity = "maker",
            CreatedAt = 1700000000000L
        };

        var trade = mapper.Map<FillDto, Trade>(dto);

        trade.IsBuyerMaker.Should().BeTrue();
    }

    [Fact]
    public void FillProfile_SellMaker_IsBuyerMakerFalse()
    {
        // sell + maker: the buyer was the taker (IsBuyerMaker = false)
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new FillDto
        {
            TradeId = "trade-3",
            OrderId = "ord-3",
            Symbol = "BTC-USDT",
            Price = "42000.0",
            Size = "0.1",
            Side = "sell",
            Liquidity = "maker",
            CreatedAt = 1700000000000L
        };

        var trade = mapper.Map<FillDto, Trade>(dto);

        // sell + maker: IsBuyerMaker = NOT (liquidity != "maker") = NOT false = false
        // Our expression: s.Side == "buy" ? s.Liquidity == "maker" : s.Liquidity != "maker"
        // sell + maker: s.Liquidity != "maker" = false → IsBuyerMaker = false
        trade.IsBuyerMaker.Should().BeFalse();
    }

    [Fact]
    public void FillProfile_SellTaker_IsBuyerMakerTrue()
    {
        // sell + taker: the buyer was the maker (IsBuyerMaker = true)
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new FillDto
        {
            TradeId = "trade-4",
            OrderId = "ord-4",
            Symbol = "BTC-USDT",
            Price = "42000.0",
            Size = "0.1",
            Side = "sell",
            Liquidity = "taker",
            CreatedAt = 1700000000000L
        };

        var trade = mapper.Map<FillDto, Trade>(dto);

        // sell + taker: s.Liquidity != "maker" = true → IsBuyerMaker = true
        trade.IsBuyerMaker.Should().BeTrue();
    }
}
