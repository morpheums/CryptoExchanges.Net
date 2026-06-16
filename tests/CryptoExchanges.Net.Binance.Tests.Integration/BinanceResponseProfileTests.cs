using Xunit;
using FluentAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Binance.Mapping;
using CryptoExchanges.Net.Binance.Services;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

/// <summary>
/// No-network unit tests for the DeltaMapper profile that maps Binance response DTOs to
/// domain models. Exercises the mappings with known inputs (not just structural validation).
/// </summary>
public class BinanceResponseProfileTests
{
    private static IMapper BuildMapper(out Symbol btcusdt)
    {
        btcusdt = new Symbol(Asset.Btc, Asset.Usdt);

        // Warm the symbol mapper so FromWire("BTCUSDT") resolves exactly (BTC/USDT happens to
        // be in the fallback list too, but warming keeps the test independent of that detail).
        var symbolMapper = new BinanceSymbolMapper();
        symbolMapper.Update([new SymbolInfo(btcusdt, [OrderType.Limit])]);

        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new BinanceResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    [Fact]
    public void OrderMapping_ResolvesSymbolAndParsesScalars()
    {
        var mapper = BuildMapper(out var btcusdt);

        var dto = new BinanceOrderResponse
        {
            Symbol = "BTCUSDT",
            OrderId = 12345,
            ClientOrderId = "abc",
            Price = "100.5",
            OrigQty = "2",
            ExecutedQty = "1",
            CumulativeQuoteQty = "150.75",
            Status = "PARTIALLY_FILLED",
            Type = "LIMIT",
            Side = "BUY",
            TimeInForce = "GTC",
            Time = 1700000000000,
            UpdateTime = 1700000001000
        };

        var order = mapper.Map<BinanceOrderResponse, Order>(dto);

        order.Symbol.Should().Be(btcusdt);
        order.OrderId.Should().Be("12345");
        order.ClientOrderId.Should().Be("abc");
        order.Price.Should().Be(100.5m);
        order.OriginalQuantity.Should().Be(2m);
        order.ExecutedQuantity.Should().Be(1m);
        order.Status.Should().Be(OrderStatus.PartiallyFilled);
        order.Side.Should().Be(OrderSide.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.TimeInForce.Should().Be(TimeInForce.Gtc);
        order.CumulativeQuoteQuantity.Should().Be(150.75m);
        order.CreatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        order.UpdatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000001000));
    }

    [Fact]
    public void OrderMapping_UnknownStatusMapsToUnknown()
    {
        var mapper = BuildMapper(out _);

        var dto = new BinanceOrderResponse
        {
            Symbol = "BTCUSDT",
            OrderId = 1,
            Status = "SOME_FUTURE_STATUS"
        };

        var order = mapper.Map<BinanceOrderResponse, Order>(dto);

        order.Status.Should().Be(OrderStatus.Unknown);
    }

    [Fact]
    public void OrderMapping_ZeroTimeYieldsNullCreatedAt()
    {
        var mapper = BuildMapper(out _);

        var dto = new BinanceOrderResponse
        {
            Symbol = "BTCUSDT",
            OrderId = 1,
            Time = 0,
            UpdateTime = 0
        };

        var order = mapper.Map<BinanceOrderResponse, Order>(dto);

        order.CreatedAt.Should().BeNull();
        order.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void TickerMapping_ParsesFieldsAndPositiveCloseTime()
    {
        var mapper = BuildMapper(out var btcusdt);

        var dto = new BinanceTickerResponse
        {
            Symbol = "BTCUSDT",
            LastPrice = "42000.1",
            OpenPrice = "41000",
            HighPrice = "43000",
            LowPrice = "40000",
            Volume = "123.45",
            QuoteVolume = "5000000",
            PriceChange = "1000.1",
            PriceChangePercent = "2.5",
            CloseTime = 1700000000000
        };

        var ticker = mapper.Map<BinanceTickerResponse, Ticker>(dto);

        ticker.Symbol.Should().Be(btcusdt);
        ticker.LastPrice.Should().Be(42000.1m);
        ticker.OpenPrice.Should().Be(41000m);
        ticker.Volume.Should().Be(123.45m);
        ticker.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public void TickerMapping_ZeroCloseTimeYieldsNullTimestamp()
    {
        var mapper = BuildMapper(out _);

        var dto = new BinanceTickerResponse
        {
            Symbol = "BTCUSDT",
            LastPrice = "42000",
            CloseTime = 0
        };

        var ticker = mapper.Map<BinanceTickerResponse, Ticker>(dto);

        ticker.Timestamp.Should().BeNull();
    }

    [Fact]
    public void BalanceMapping_KnownTicker_MapsToTypedAsset()
    {
        var mapper = BuildMapper(out _);

        var dto = new BinanceBalance { Asset = "BTC", Free = "1.5", Locked = "0.25" };

        var balance = mapper.Map<BinanceBalance, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(1.5m);
        balance.Locked.Should().Be(0.25m);
        balance.Total.Should().Be(1.75m);
    }

    [Fact]
    public void BalanceMapping_UnrepresentableTicker_MapsToAssetNone()
    {
        var mapper = BuildMapper(out _);

        // A ticker with characters outside A-Z/0-9 is not representable as an Asset; it must
        // map to Asset.None rather than throwing (balances are where long-tail assets appear).
        var dto = new BinanceBalance { Asset = "bad-ticker!", Free = "3", Locked = "0" };

        var balance = mapper.Map<BinanceBalance, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.None);
        balance.Asset.IsNone.Should().BeTrue();
        balance.Free.Should().Be(3m);
    }
}
