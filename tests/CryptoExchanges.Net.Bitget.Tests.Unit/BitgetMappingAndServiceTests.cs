using Xunit;
using AwesomeAssertions;
using NSubstitute;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using CryptoExchanges.Net.Bitget;
using CryptoExchanges.Net.Bitget.Internal;
using CryptoExchanges.Net.Bitget.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Bitget.Tests.Unit;

/// <summary>
/// No-network unit tests for the Bitget DeltaMapper profile (DTO -> domain model) and the three
/// services exercised over a mocked <see cref="IBitgetHttpClient"/> (NSubstitute). Includes the
/// market-order round-trip regression (TASK-013/020 carry) and DI resolution (incl. the
/// secret+passphrase gate).
/// </summary>
public class BitgetMappingAndServiceTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (ISymbolMapper symbolMapper, IMapper mapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(BitgetSymbolFormat.Instance);
        // Warm so FromWire("BTCUSDT") resolves to BTC/USDT exactly.
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = BitgetClientComposer.CreateMapper(symbolMapper);
        return (symbolMapper, mapper);
    }

    // ── DeltaMapper config validity ──

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        // CreateMapper invokes AssertConfigurationIsValid internally; if any member is unmapped it throws.
        var act = () => BuildMappers();
        act.Should().NotThrow();
    }

    // ── Profile mapping (representative V2 payloads) ──

    [Fact]
    public void OrderProfile_MapsAllScalarsAndResolvesSymbol()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OrderDto
        {
            Symbol = "BTCUSDT",
            OrderId = "111",
            ClientOid = "cli-1",
            Price = "100.5",
            Size = "2",
            BaseVolume = "1",
            QuoteVolume = "150.75",
            PriceAvg = "150.75",
            Side = "buy",
            OrderType = "limit",
            Force = "gtc",
            Status = "partially_filled",
            CTime = "1700000000000",
            UTime = "1700000001000"
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.Symbol.Should().Be(BtcUsdt);
        order.OrderId.Should().Be("111");
        order.ClientOrderId.Should().Be("cli-1");
        order.Price.Should().Be(100.5m);
        order.OriginalQuantity.Should().Be(2m);
        order.ExecutedQuantity.Should().Be(1m);
        // CumulativeQuoteQuantity comes straight from Bitget's quoteVolume.
        order.CumulativeQuoteQuantity.Should().Be(150.75m);
        order.Side.Should().Be(OrderSide.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.Status.Should().Be(OrderStatus.PartiallyFilled);
        order.TimeInForce.Should().Be(TimeInForce.Gtc);
        order.CreatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        order.UpdatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000001000));
    }

    // ── MARKET-ORDER round-trip regression (TASK-013/020): a market order maps cleanly ──

    [Fact]
    public void OrderProfile_MarketOrder_MapsCleanly()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OrderDto
        {
            Symbol = "BTCUSDT",
            OrderId = "222",
            Size = "0.5",
            BaseVolume = "0.5",
            QuoteVolume = "21000",
            Side = "buy",
            OrderType = "market",
            Force = "gtc",
            Status = "filled",
            CTime = "1700000000000",
            UTime = "1700000000500"
        };

        var act = () => mapper.Map<OrderDto, Order>(dto);
        act.Should().NotThrow();

        var order = mapper.Map<OrderDto, Order>(dto);
        order.Type.Should().Be(OrderType.Market);
        order.Status.Should().Be(OrderStatus.Filled);
        order.CumulativeQuoteQuantity.Should().Be(21000m);
    }

    [Fact]
    public void TickerProfile_ComputesChangeFromChange24h()
    {
        var (_, mapper) = BuildMappers();
        var dto = new TickerDto
        {
            Symbol = "BTCUSDT",
            LastPr = "42000",
            Open = "40000",
            High24h = "43000",
            Low24h = "39000",
            BaseVolume = "123.45",
            QuoteVolume = "5000000",
            Change24h = "0.05",
            Ts = "1700000000000"
        };

        var ticker = mapper.Map<TickerDto, Ticker>(dto);

        ticker.Symbol.Should().Be(BtcUsdt);
        ticker.LastPrice.Should().Be(42000m);
        ticker.OpenPrice.Should().Be(40000m);
        ticker.PriceChange.Should().Be(2000m);
        // Bitget reports change24h fractional (0.05) -> 5%.
        ticker.PriceChangePercent.Should().Be(5m);
        ticker.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public void SymbolProfile_MapsBaseQuoteToSymbol()
    {
        var (_, mapper) = BuildMappers();
        var dto = new SymbolInfoDto { Symbol = "BTCUSDT", BaseCoin = "BTC", QuoteCoin = "USDT" };

        var info = mapper.Map<SymbolInfoDto, SymbolInfo>(dto);

        info.Symbol.Should().Be(BtcUsdt);
        info.AllowedOrderTypes.Should().Contain(OrderType.Limit).And.Contain(OrderType.Market);
    }

    [Fact]
    public void BalanceProfile_MapsAvailableAndCombinesFrozenLocked()
    {
        var (_, mapper) = BuildMappers();
        var dto = new BalanceDto { Coin = "BTC", Available = "1.5", Frozen = "0.25", Locked = "0.1" };

        var balance = mapper.Map<BalanceDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(1.5m);
        // Domain Locked combines Bitget's frozen + locked.
        balance.Locked.Should().Be(0.35m);
        balance.Total.Should().Be(1.85m);
    }

    // ── MarketDataService over mocked IBitgetHttpClient ──

    [Fact]
    public async Task MarketData_GetTickers_SingleSymbol_MapsPayload()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        http.GetAsync<ResponseDto<TickerDto>>(
                "/api/v2/spot/market/tickers", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<TickerDto>
            {
                Data = [new TickerDto { Symbol = "BTCUSDT", LastPr = "42000", Open = "40000", Change24h = "0.05" }]
            });

        var service = new BitgetMarketDataService(http, symbolMapper, mapper);
        var tickers = await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        tickers.Should().HaveCount(1);
        tickers[0].Symbol.Should().Be(BtcUsdt);
        tickers[0].LastPrice.Should().Be(42000m);
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_HappyPath_MapsOhlcvAndOpenTime()
    {
        // Column order from BitgetMarketDataService mapping: arr[0]=ts, arr[1]=open, arr[2]=high,
        // arr[3]=low, arr[4]=close, arr[5]=baseVolume, arr[6]=quoteVolume.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        http.GetAsync<ResponseDto<List<string>>>(
                "/api/v2/spot/market/candles", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<string>>
            {
                Data = [["1700000000000", "42000", "43000", "41000", "42500", "10", "420000"]]
            });

        var service = new BitgetMarketDataService(http, symbolMapper, mapper);
        var candles = await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneHour, ct: TestContext.Current.CancellationToken);

        candles.Should().HaveCount(1);
        candles[0].OpenTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        candles[0].Open.Should().Be(42000m);
        candles[0].Volume.Should().Be(10m);
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_EmptyTimestamp_MapsToEpochZero()
    {
        // BitgetValueParsers.ParseMs("") returns 0L (safe TryParse); long.Parse would have thrown.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        http.GetAsync<ResponseDto<List<string>>>(
                "/api/v2/spot/market/candles", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<string>>
            {
                Data = [["", "42000", "43000", "41000", "42500", "10", "420000"]]
            });

        var service = new BitgetMarketDataService(http, symbolMapper, mapper);
        var candles = await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneMinute, ct: TestContext.Current.CancellationToken);
        candles.Should().HaveCount(1);
        candles[0].OpenTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(0L));
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_LargeLimit_ClampsToThousand()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<List<string>>>(
                "/api/v2/spot/market/candles", Arg.Do<Dictionary<string, string>>(p => captured = p), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<string>>());

        var service = new BitgetMarketDataService(http, symbolMapper, mapper);
        var act = async () => await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneHour, limit: 5000, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("1000");
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_TwoHoursInterval_ThrowsArgumentOutOfRange()
    {
        // MapKlineInterval has no 2h arm; it throws ArgumentOutOfRangeException.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();

        var service = new BitgetMarketDataService(http, symbolMapper, mapper);
        var act = async () => await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.TwoHours, ct: TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task MarketData_GetOrderBook_ParsesLevels()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        http.GetAsync<ResponseDto<OrderBookDto>>(
                "/api/v2/spot/market/orderbook", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderBookDto>
            {
                Data =
                [
                    new OrderBookDto
                    {
                        Bids = [["100", "2"]],
                        Asks = [["101", "3"]],
                        Ts = "1700000000000"
                    }
                ]
            });

        var service = new BitgetMarketDataService(http, symbolMapper, mapper);
        var book = await service.GetOrderBookAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);

        book.Bids.Should().ContainSingle().Which.Price.Should().Be(100m);
        book.Asks.Should().ContainSingle().Which.Quantity.Should().Be(3m);
    }

    [Fact]
    public async Task MarketData_GetOrderBook_SkipsShortLevels()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        http.GetAsync<ResponseDto<OrderBookDto>>(
                "/api/v2/spot/market/orderbook", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderBookDto>
            {
                Data =
                [
                    new OrderBookDto
                    {
                        // Malformed/short rows (fewer than [price, size]) must be skipped, not throw.
                        Bids = [["100", "2"], [], ["99"]],
                        Asks = [["101"], ["102", "3"]],
                        Ts = "1700000000000"
                    }
                ]
            });

        var service = new BitgetMarketDataService(http, symbolMapper, mapper);
        var book = await service.GetOrderBookAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);

        book.Bids.Should().ContainSingle().Which.Price.Should().Be(100m);
        book.Asks.Should().ContainSingle().Which.Price.Should().Be(102m);
    }

    // ── AccountService over mocked IBitgetHttpClient ──

    [Fact]
    public async Task Account_GetBalances_TrimsZeroBalances()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        http.GetAsync<ResponseDto<BalanceDto>>(
                "/api/v2/spot/account/assets", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<BalanceDto>
            {
                Data =
                [
                    new BalanceDto { Coin = "BTC", Available = "1.5", Frozen = "0", Locked = "0" },
                    new BalanceDto { Coin = "ZZZ", Available = "0", Frozen = "0", Locked = "0" }
                ]
            });

        var service = new BitgetAccountService(http, symbolMapper, mapper);
        var balances = await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        balances.Should().HaveCount(1);
        balances[0].Asset.Should().Be(Asset.Btc);
        balances[0].Total.Should().Be(1.5m);
    }

    [Fact]
    public async Task Account_GetTradeHistory_DefaultLimit_ClampsToHundred()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<FillDto>>(
                "/api/v2/spot/trade/fills", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<FillDto>());

        var service = new BitgetAccountService(http, symbolMapper, mapper);

        // Default IExchangeClient limit is 500; the service must clamp to 100, not throw.
        var act = async () => await service.GetTradeHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("100");
    }

    // ── TradingService over mocked IBitgetHttpClient ──

    [Fact]
    public async Task Trading_GetOpenOrders_MapsOrders()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        http.GetAsync<ResponseDto<OrderDto>>(
                "/api/v2/spot/trade/unfilled-orders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = [new OrderDto { Symbol = "BTCUSDT", OrderId = "9", Size = "1", Price = "5" }]
            });

        var service = new BitgetTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOpenOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        orders.Should().HaveCount(1);
        orders[0].OrderId.Should().Be("9");
        orders[0].Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task Trading_GetOrderHistory_DefaultLimit_ClampsToHundred()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<OrderDto>>(
                "/api/v2/spot/trade/history-orders", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>());

        var service = new BitgetTradingService(http, symbolMapper, mapper);

        var act = async () => await service.GetOrderHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("100");
    }

    [Fact]
    public async Task Trading_PlaceMarketOrder_SendsMarketOrderTypeAndRefetches()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();

        Dictionary<string, string>? placed = null;
        http.PostAsync<ResponseDto<OrderAckDto>>(
                "/api/v2/spot/trade/place-order", Arg.Do<Dictionary<string, string>>(p => placed = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderAckDto> { Data = [new OrderAckDto { OrderId = "ord-1" }] });

        http.GetAsync<ResponseDto<OrderDto>>(
                "/api/v2/spot/trade/orderInfo", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = [new OrderDto { Symbol = "BTCUSDT", OrderId = "ord-1", Size = "0.5", OrderType = "market", Status = "filled", Side = "buy" }]
            });

        var service = new BitgetTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Market, quantity: 0.5m);
        var order = await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placed.Should().NotBeNull();
        placed!["orderType"].Should().Be("market");
        placed["side"].Should().Be("buy");
        placed.Should().NotContainKey("price"); // market orders carry no price
        order.OrderId.Should().Be("ord-1");
        order.Type.Should().Be(OrderType.Market);
    }

    [Fact]
    public async Task Trading_CancelByClientId_AckOmitsOrderId_RefetchesByClientOid()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBitgetHttpClient>();

        // The cancel ack omits orderId (only clientOid is echoed).
        http.PostAsync<ResponseDto<OrderAckDto>>(
                "/api/v2/spot/trade/cancel-order", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderAckDto> { Data = [new OrderAckDto { OrderId = string.Empty, ClientOid = "cli-77" }] });

        Dictionary<string, string>? refetchParams = null;
        http.GetAsync<ResponseDto<OrderDto>>(
                "/api/v2/spot/trade/orderInfo", Arg.Do<Dictionary<string, string>>(p => refetchParams = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = [new OrderDto { Symbol = "BTCUSDT", OrderId = "real-99", ClientOid = "cli-77", Status = "cancelled" }]
            });

        var service = new BitgetTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "cli-77", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("real-99");
        order.OrderId.Should().NotBeNullOrEmpty();
        refetchParams.Should().NotBeNull();
        refetchParams!.Should().ContainKey("clientOid").WhoseValue.Should().Be("cli-77");
        refetchParams.Should().NotContainKey("orderId");
    }

    // ── DI resolution ──

    [Fact]
    public async Task Di_AddBitgetExchange_ResolvesKeyedClient()
    {
        var services = new ServiceCollection();
        services.AddBitgetExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bitget);
        client.ExchangeId.Should().Be(ExchangeId.Bitget);
    }

    [Fact]
    public async Task Di_AddBitgetExchange_Secretless_StillResolvesWorkingClient()
    {
        // A secretless registration must resolve (public market data needs no credentials); the
        // finalizer is a PassThroughHandler rather than a signing handler in this path.
        var services = new ServiceCollection();
        services.AddBitgetExchange();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bitget).ExchangeId.Should().Be(ExchangeId.Bitget);
    }

    [Fact]
    public async Task Di_AddBitgetExchange_PassphraseMissing_StillResolves()
    {
        // Secret present but passphrase missing: signing is gated OFF (PassThrough), and the client must
        // still resolve so this does NOT trip BitgetOptions.ToCredentials() (which throws on empty
        // passphrase). This is the TASK-010/017 carry-in.
        var services = new ServiceCollection();
        services.AddBitgetExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; /* no passphrase */ });
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bitget).ExchangeId.Should().Be(ExchangeId.Bitget);
    }

    [Fact]
    public void Di_AddBitgetExchange_MapperIsKeyedSingleton()
    {
        var services = new ServiceCollection();
        services.AddBitgetExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        using var sp = services.BuildServiceProvider();

        var m1 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Bitget);
        var m2 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Bitget);
        m1.Should().BeSameAs(m2);
    }

    [Fact]
    public void Di_AddBitgetExchange_InvalidOptions_FailFast()
    {
        var services = new ServiceCollection();
        services.AddBitgetExchange(o => o.TimeoutSeconds = 0);
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bitget);
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public async Task Di_AddBitgetExchange_IsScopeClean()
    {
        var services = new ServiceCollection();
        services.AddBitgetExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        await using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bitget).Should().NotBeNull();
    }

    [Fact]
    public void Di_AddBitgetExchange_BaseUrlWithPath_FailFast()
    {
        // TASK-021 CONCERN#1: a BaseUrl carrying a path segment breaks the sign-consistency invariant,
        // so NormalizeHostRoot (invoked during option validation) must fail fast rather than silently
        // produce rejected signatures at runtime.
        var services = new ServiceCollection();
        services.AddBitgetExchange(o => o.BaseUrl = "https://api.bitget.com/api/v2");
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bitget);
        act.Should().Throw<Exception>();
    }
}
