using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Kraken;
using CryptoExchanges.Net.Kraken.Dtos;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Kraken.Mapping;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kraken.Tests.Unit;

/// <summary>
/// No-network unit tests for:
/// <list type="bullet">
///   <item><see cref="KrakenSymbolFormat"/> alias round-trips via the generic <see cref="SymbolMapper"/>.</item>
///   <item><see cref="KrakenValueParsers"/> decimal, enum, and timestamp helpers.</item>
///   <item>DTO JSON deserialization round-trips (representative Kraken payloads).</item>
///   <item><see cref="KrakenResponseProfile"/> DeltaMapper DTO→model assertions.</item>
/// </list>
/// </summary>
public class KrakenSymbolFormatTests
{
    private static readonly Symbol BtcUsdt  = new(Asset.Btc,  Asset.Usdt);
    private static readonly Symbol EthUsdt  = new(Asset.Eth,  Asset.Usdt);
    private static readonly Symbol DogeUsdt = new(Asset.Of("DOGE"), Asset.Usdt);

    private static SymbolMapper BuildMapper(params Symbol[] symbols)
    {
        var mapper = new SymbolMapper(KrakenSymbolFormat.Instance);
        if (symbols.Length > 0)
            mapper.UpdateSymbols(symbols.Select(s => new SymbolInfo(s, [OrderType.Limit, OrderType.Market])));
        return mapper;
    }

    private static IMapper BuildDeltaMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new KrakenResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    [Fact]
    public void ToWire_BtcUsdt_ReturnsXbtSlashUsdt()
    {
        var mapper = BuildMapper();
        mapper.ToWire(BtcUsdt).Should().Be("XBT/USDT");
    }

    [Fact]
    public void ToWire_DogeUsdt_ReturnsXdgSlashUsdt()
    {
        var mapper = BuildMapper();
        mapper.ToWire(DogeUsdt).Should().Be("XDG/USDT");
    }

    [Fact]
    public void ToWire_EthUsdt_ReturnsEthSlashUsdt()
    {
        var mapper = BuildMapper();
        mapper.ToWire(EthUsdt).Should().Be("ETH/USDT");
    }

    [Fact]
    public void FromWire_XbtSlashUsd_ReturnsBtcUsd()
    {
        var mapper = BuildMapper();
        var result = mapper.FromWire("XBT/USD");
        result.Should().Be(new Symbol(Asset.Btc, Asset.Of("USD")));
    }

    [Fact]
    public void FromWire_XbtSlashUsdt_ReturnsBtcUsdt()
    {
        var mapper = BuildMapper(BtcUsdt);
        mapper.FromWire("XBT/USDT").Should().Be(BtcUsdt);
    }

    [Fact]
    public void FromWire_XdgSlashUsdt_ReturnsDogeUsdt()
    {
        var mapper = BuildMapper(DogeUsdt);
        mapper.FromWire("XDG/USDT").Should().Be(DogeUsdt);
    }

    [Fact]
    public void FromWire_EthSlashUsdt_ReturnsEthUsdt()
    {
        var mapper = BuildMapper();
        mapper.FromWire("ETH/USDT").Should().Be(EthUsdt);
    }

    [Fact]
    public void FromWire_NullOrEmpty_ThrowsArgumentException()
    {
        var mapper = BuildMapper();
        var act = () => mapper.FromWire("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParseDecimal_ValidString_ReturnsParsedValue()
        => KrakenValueParsers.ParseDecimal("123.45").Should().Be(123.45m);

    [Fact]
    public void ParseDecimal_EmptyString_ReturnsZero()
        => KrakenValueParsers.ParseDecimal("").Should().Be(0m);

    [Fact]
    public void ParseDecimal_NullString_ReturnsZero()
        => KrakenValueParsers.ParseDecimal(null!).Should().Be(0m);

    [Fact]
    public void ParseOptionalDecimal_ValidString_ReturnsParsedValue()
        => KrakenValueParsers.ParseOptionalDecimal("50.0").Should().Be(50.0m);

    [Fact]
    public void ParseOptionalDecimal_ZeroString_ReturnsNull()
        => KrakenValueParsers.ParseOptionalDecimal("0").Should().BeNull();

    [Fact]
    public void ParseOrderSide_Buy_ReturnsBuy()
        => KrakenValueParsers.ParseOrderSide("buy").Should().Be(OrderSide.Buy);

    [Fact]
    public void ParseOrderSide_Sell_ReturnsSell()
        => KrakenValueParsers.ParseOrderSide("sell").Should().Be(OrderSide.Sell);

    [Fact]
    public void ParseOrderSide_Unknown_ThrowsArgumentOutOfRange()
    {
        var act = () => KrakenValueParsers.ParseOrderSide("unknown");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseOrderType_Limit_ReturnsLimit()
        => KrakenValueParsers.ParseOrderType("limit").Should().Be(OrderType.Limit);

    [Fact]
    public void ParseOrderType_Market_ReturnsMarket()
        => KrakenValueParsers.ParseOrderType("market").Should().Be(OrderType.Market);

    [Fact]
    public void ParseOrderType_Unknown_ThrowsArgumentOutOfRange()
    {
        var act = () => KrakenValueParsers.ParseOrderType("stop");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("pending", OrderStatus.New)]
    [InlineData("open", OrderStatus.New)]
    [InlineData("closed", OrderStatus.Filled)]
    [InlineData("canceled", OrderStatus.Canceled)]
    [InlineData("expired", OrderStatus.Canceled)]
    [InlineData("unknown_future", OrderStatus.Unknown)]
    public void ParseOrderStatus_ReturnsExpected(string input, OrderStatus expected)
        => KrakenValueParsers.ParseOrderStatus(input).Should().Be(expected);

    [Fact]
    public void ParseFractionalSecondsToMs_ValidValue_ConvertsToMs()
        => KrakenValueParsers.ParseFractionalSecondsToMs(1700000000.500m).Should().Be(1700000000500L);

    [Fact]
    public void ParseSecondsToMs_ValidValue_ConvertsToMs()
        => KrakenValueParsers.ParseSecondsToMs(1700000000L).Should().Be(1700000000000L);

    [Fact]
    public void ParseTradeSide_B_ReturnsBuy()
        => KrakenValueParsers.ParseTradeSide("b").Should().Be(OrderSide.Buy);

    [Fact]
    public void ParseTradeSide_S_ReturnsSell()
        => KrakenValueParsers.ParseTradeSide("s").Should().Be(OrderSide.Sell);

    [Fact]
    public void ParseAssetOrNone_BTC_ReturnsBtcAsset()
        => KrakenValueParsers.ParseAssetOrNone("BTC").Should().Be(Asset.Btc);

    [Fact]
    public void ParseAssetOrNone_Empty_ReturnsNone()
        => KrakenValueParsers.ParseAssetOrNone("").Should().Be(Asset.None);

    [Fact]
    public void ServerTimeDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "unixtime": 1700000000,
              "rfc1123": "Wed, 15 Nov 2023 00:00:00 +0000"
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<ServerTimeDto>(json);

        dto.Should().NotBeNull();
        dto!.UnixTime.Should().Be(1700000000L);
        dto.Rfc1123.Should().Be("Wed, 15 Nov 2023 00:00:00 +0000");
    }

    [Fact]
    public void SymbolInfoDto_Deserializes_FromRepresentativeJson()
    {
        const string json = """
            {
              "wsname": "XBT/USDT",
              "base": "XXBT",
              "quote": "ZUSDT",
              "ordermin": "0.0001",
              "pair_decimals": 1,
              "lot_decimals": 8
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<SymbolInfoDto>(json);

        dto.Should().NotBeNull();
        dto!.Wsname.Should().Be("XBT/USDT");
        dto.Base.Should().Be("XXBT");
        dto.OrderMin.Should().Be("0.0001");
    }

    [Fact]
    public void ResponseDto_Deserializes_EnvelopeWithResult()
    {
        const string json = """
            {
              "error": [],
              "result": {
                "unixtime": 1700000000,
                "rfc1123": "Wed, 15 Nov 2023 00:00:00 +0000"
              }
            }
            """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<ResponseDto<ServerTimeDto>>(json);

        dto.Should().NotBeNull();
        dto!.Error.Should().BeEmpty();
        dto.Result.Should().NotBeNull();
        dto.Result!.UnixTime.Should().Be(1700000000L);
    }

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var act = () => BuildDeltaMapper(symbolMapper);
        act.Should().NotThrow();
    }

    [Fact]
    public void SymbolInfoProfile_MapsWsnameToSymbol()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new SymbolInfoDto { Wsname = "XBT/USDT", Base = "XXBT", Quote = "ZUSDT", OrderMin = "0.0001" };

        var info = mapper.Map<SymbolInfoDto, SymbolInfo>(dto);

        info.Symbol.Should().Be(BtcUsdt);
        info.AllowedOrderTypes.Should().Contain(OrderType.Limit).And.Contain(OrderType.Market);
        info.MinQuantity.Should().Be(0.0001m);
    }

    [Fact]
    public void BalanceProfile_MapsAssetAndFree()
    {
        var symbolMapper = BuildMapper();
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new BalanceDto { Asset = "BTC", Balance = "1.5" };

        var balance = mapper.Map<BalanceDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(1.5m);
        balance.Locked.Should().Be(0m);
        balance.Total.Should().Be(1.5m);
    }

    [Fact]
    public void BalanceProfile_InvalidCurrency_MapsToAssetNone()
    {
        var symbolMapper = BuildMapper();
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new BalanceDto { Asset = "INVALID!@#", Balance = "10" };

        var balance = mapper.Map<BalanceDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.None);
    }

    [Fact]
    public void FillProfile_BuyMaker_IsBuyerMakerTrue()
    {
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new FillDto
        {
            Pair = "XBT/USDT",
            Time = 1700000000.5m,
            Side = "buy",
            Price = "42000.0",
            Volume = "0.1",
            OrderTxId = "ord-1",
            Maker = true
        };

        var trade = mapper.Map<FillDto, Trade>(dto);

        trade.Symbol.Should().Be(BtcUsdt);
        trade.Price.Should().Be(42000m);
        trade.Quantity.Should().Be(0.1m);
        trade.IsBuyerMaker.Should().BeTrue();
        trade.OrderId.Should().Be("ord-1");
        trade.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000500L));
    }

    [Fact]
    public void FillProfile_SellTaker_IsBuyerMakerTrue()
    {
        // sell + taker (Maker=false): buyer was the maker
        var symbolMapper = BuildMapper(BtcUsdt);
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new FillDto { Pair = "XBT/USDT", Side = "sell", Price = "42000", Volume = "0.1", Maker = false };

        var trade = mapper.Map<FillDto, Trade>(dto);

        trade.IsBuyerMaker.Should().BeTrue();
    }

    [Fact]
    public void CandlestickProfile_MapsOhlcFields()
    {
        var symbolMapper = BuildMapper();
        var mapper = BuildDeltaMapper(symbolMapper);
        var dto = new CandlestickDto
        {
            OpenTime = 1700000000L,
            Open = "41000",
            High = "43000",
            Low = "40000",
            Close = "42000",
            Volume = "123.45",
            Count = 500
        };

        var candle = mapper.Map<CandlestickDto, Candlestick>(dto);

        candle.OpenTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000L));
        candle.Open.Should().Be(41000m);
        candle.High.Should().Be(43000m);
        candle.Low.Should().Be(40000m);
        candle.Close.Should().Be(42000m);
        candle.Volume.Should().Be(123.45m);
        candle.TradeCount.Should().Be(500);
    }
}
