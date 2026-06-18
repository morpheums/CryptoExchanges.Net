using Xunit;
using FluentAssertions;
using NSubstitute;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using CryptoExchanges.Net.Okx;
using CryptoExchanges.Net.Okx.Internal;
using CryptoExchanges.Net.Okx.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.DependencyInjection;

namespace CryptoExchanges.Net.Okx.Tests.Unit;

/// <summary>
/// No-network unit tests for the OKX DeltaMapper profile (DTO -> domain model) and the three services
/// exercised over a mocked <see cref="IOkxHttpClient"/> (NSubstitute). Includes the market-order
/// round-trip regression (TASK-013 carry) and DI resolution (incl. the secret+passphrase gate).
/// </summary>
public class OkxMappingAndServiceTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (ISymbolMapper symbolMapper, IMapper mapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(OkxSymbolFormat.Instance);
        // Warm so FromWire("BTC-USDT") resolves to BTC/USDT exactly.
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = OkxClientComposer.CreateMapper(symbolMapper);
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

    // ── Profile mapping (representative V5 payloads) ──

    [Fact]
    public void OrderProfile_MapsAllScalarsAndResolvesSymbol()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OkxOrder
        {
            InstId = "BTC-USDT",
            OrdId = "111",
            ClOrdId = "cli-1",
            Px = "100.5",
            Sz = "2",
            AccFillSz = "1",
            AvgPx = "150.75",
            Side = "buy",
            OrdType = "limit",
            State = "partially_filled",
            CTime = "1700000000000",
            UTime = "1700000001000"
        };

        var order = mapper.Map<OkxOrder, Order>(dto);

        order.Symbol.Should().Be(BtcUsdt);
        order.OrderId.Should().Be("111");
        order.ClientOrderId.Should().Be("cli-1");
        order.Price.Should().Be(100.5m);
        order.OriginalQuantity.Should().Be(2m);
        order.ExecutedQuantity.Should().Be(1m);
        // CumulativeQuoteQuantity = accFillSz * avgPx = 1 * 150.75.
        order.CumulativeQuoteQuantity.Should().Be(150.75m);
        order.Side.Should().Be(OrderSide.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.Status.Should().Be(OrderStatus.PartiallyFilled);
        order.TimeInForce.Should().Be(TimeInForce.Gtc);
        order.CreatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        order.UpdatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000001000));
    }

    // ── MARKET-ORDER round-trip regression (TASK-013): a market order maps cleanly ──

    [Fact]
    public void OrderProfile_MarketOrder_MapsCleanly()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OkxOrder
        {
            InstId = "BTC-USDT",
            OrdId = "222",
            Sz = "0.5",
            AccFillSz = "0.5",
            AvgPx = "42000",
            Side = "buy",
            OrdType = "market",
            State = "filled",
            CTime = "1700000000000",
            UTime = "1700000000500"
        };

        // ParseOrderType("market") -> Market and ParseTimeInForce("market") -> Ioc must both succeed.
        var act = () => mapper.Map<OkxOrder, Order>(dto);
        act.Should().NotThrow();

        var order = mapper.Map<OkxOrder, Order>(dto);
        order.Type.Should().Be(OrderType.Market);
        order.TimeInForce.Should().Be(TimeInForce.Ioc);
        order.Status.Should().Be(OrderStatus.Filled);
        order.CumulativeQuoteQuantity.Should().Be(21000m); // 0.5 * 42000
    }

    [Fact]
    public void TickerProfile_ComputesChangeFromOpen24h()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OkxTicker
        {
            InstId = "BTC-USDT",
            Last = "42000",
            Open24h = "40000",
            High24h = "43000",
            Low24h = "39000",
            Vol24h = "123.45",
            VolCcy24h = "5000000",
            Ts = "1700000000000"
        };

        var ticker = mapper.Map<OkxTicker, Ticker>(dto);

        ticker.Symbol.Should().Be(BtcUsdt);
        ticker.LastPrice.Should().Be(42000m);
        ticker.OpenPrice.Should().Be(40000m);
        ticker.PriceChange.Should().Be(2000m);
        // (42000 - 40000) / 40000 * 100 = 5%.
        ticker.PriceChangePercent.Should().Be(5m);
        ticker.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public void InstrumentProfile_MapsBaseQuoteToSymbol()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OkxInstrument { InstId = "BTC-USDT", BaseCcy = "BTC", QuoteCcy = "USDT" };

        var info = mapper.Map<OkxInstrument, SymbolInfo>(dto);

        info.Symbol.Should().Be(BtcUsdt);
        info.AllowedOrderTypes.Should().Contain(OrderType.Limit).And.Contain(OrderType.Market);
    }

    [Fact]
    public void BalanceProfile_MapsAvailAndFrozen()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OkxBalanceDetail { Ccy = "BTC", AvailBal = "1.5", FrozenBal = "0.25" };

        var balance = mapper.Map<OkxBalanceDetail, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(1.5m);
        balance.Locked.Should().Be(0.25m);
        balance.Total.Should().Be(1.75m);
    }

    // ── MarketDataService over mocked IOkxHttpClient ──

    [Fact]
    public async Task MarketData_GetTickers_SingleSymbol_MapsPayload()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        http.GetAsync<OkxResponse<OkxTicker>>(
                "/api/v5/market/ticker", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxTicker>
            {
                Data = [new OkxTicker { InstId = "BTC-USDT", Last = "42000", Open24h = "40000" }]
            });

        var service = new OkxMarketDataService(http, symbolMapper, mapper);
        var tickers = await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        tickers.Should().HaveCount(1);
        tickers[0].Symbol.Should().Be(BtcUsdt);
        tickers[0].LastPrice.Should().Be(42000m);
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_HappyPath_MapsOhlcvAndOpenTime()
    {
        // Exercises the B1 fix: OpenTime is parsed via OkxValueParsers.ParseMs (safe TryParse).
        // Column order from OkxMarketDataService mapping: arr[0]=ts, arr[1]=open, arr[2]=high,
        // arr[3]=low, arr[4]=close, arr[5]=vol, arr[6]=volCcy.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        http.GetAsync<OkxResponse<List<string>>>(
                "/api/v5/market/candles", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<List<string>>
            {
                Data = [["1700000000000", "42000", "43000", "41000", "42500", "10", "420000"]]
            });

        var service = new OkxMarketDataService(http, symbolMapper, mapper);
        var candles = await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneHour, ct: TestContext.Current.CancellationToken);

        candles.Should().HaveCount(1);
        candles[0].OpenTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        candles[0].Open.Should().Be(42000m);
        candles[0].Volume.Should().Be(10m);
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_EmptyTimestamp_MapsToEpochZero()
    {
        // Directly regression-guards the B1 fix: OKX returns "" for unconfirmed candles.
        // The safe OkxValueParsers.ParseMs("") returns 0L; long.Parse would have thrown.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        http.GetAsync<OkxResponse<List<string>>>(
                "/api/v5/market/candles", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<List<string>>
            {
                Data = [["", "42000", "43000", "41000", "42500", "10", "420000"]]
            });

        var service = new OkxMarketDataService(http, symbolMapper, mapper);
        var act = async () => await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneMinute, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        var candles = await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneMinute, ct: TestContext.Current.CancellationToken);
        candles.Should().HaveCount(1);
        candles[0].OpenTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(0L));
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_EightHoursInterval_ThrowsArgumentOutOfRange()
    {
        // MapKlineInterval has no 8h arm; it throws ArgumentOutOfRangeException.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();

        var service = new OkxMarketDataService(http, symbolMapper, mapper);
        var act = async () => await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.EightHours, ct: TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_LargeLimit_ClampsToHundred()
    {
        // Mirrors Account_GetTradeHistory_DefaultLimit_ClampsToHundred: capture the outbound params
        // and assert the "limit" field is clamped to "100" (OkxRequestValidation.MaxHistoryLimit).
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<OkxResponse<List<string>>>(
                "/api/v5/market/candles", Arg.Do<Dictionary<string, string>>(p => captured = p), false, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<List<string>>());

        var service = new OkxMarketDataService(http, symbolMapper, mapper);
        var act = async () => await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneHour, limit: 500, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("100");
    }

    [Fact]
    public async Task MarketData_GetOrderBook_ParsesLevels()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        http.GetAsync<OkxResponse<OkxOrderBook>>(
                "/api/v5/market/books", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxOrderBook>
            {
                Data =
                [
                    new OkxOrderBook
                    {
                        Bids = [["100", "2", "0", "1"]],
                        Asks = [["101", "3", "0", "1"]],
                        Ts = "1700000000000"
                    }
                ]
            });

        var service = new OkxMarketDataService(http, symbolMapper, mapper);
        var book = await service.GetOrderBookAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);

        book.Bids.Should().ContainSingle().Which.Price.Should().Be(100m);
        book.Asks.Should().ContainSingle().Which.Quantity.Should().Be(3m);
    }

    // ── AccountService over mocked IOkxHttpClient ──

    [Fact]
    public async Task Account_GetBalances_TrimsZeroBalances()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        http.GetAsync<OkxResponse<OkxBalanceAccount>>(
                "/api/v5/account/balance", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxBalanceAccount>
            {
                Data =
                [
                    new OkxBalanceAccount
                    {
                        Details =
                        [
                            new OkxBalanceDetail { Ccy = "BTC", AvailBal = "1.5", FrozenBal = "0" },
                            new OkxBalanceDetail { Ccy = "ZZZ", AvailBal = "0", FrozenBal = "0" }
                        ]
                    }
                ]
            });

        var service = new OkxAccountService(http, symbolMapper, mapper);
        var balances = await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        balances.Should().HaveCount(1);
        balances[0].Asset.Should().Be(Asset.Btc);
        balances[0].Total.Should().Be(1.5m);
    }

    [Fact]
    public async Task Account_GetTradeHistory_DefaultLimit_ClampsToHundred()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<OkxResponse<OkxFill>>(
                "/api/v5/trade/fills", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxFill>());

        var service = new OkxAccountService(http, symbolMapper, mapper);

        // Default IExchangeClient limit is 500; the service must clamp to 100, not throw.
        var act = async () => await service.GetTradeHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("100");
    }

    // ── TradingService over mocked IOkxHttpClient ──

    [Fact]
    public async Task Trading_GetOpenOrders_MapsOrders()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        http.GetAsync<OkxResponse<OkxOrder>>(
                "/api/v5/trade/orders-pending", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxOrder>
            {
                Data = [new OkxOrder { InstId = "BTC-USDT", OrdId = "9", Sz = "1", Px = "5" }]
            });

        var service = new OkxTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOpenOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        orders.Should().HaveCount(1);
        orders[0].OrderId.Should().Be("9");
        orders[0].Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task Trading_GetOrderHistory_DefaultLimit_ClampsToHundred()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<OkxResponse<OkxOrder>>(
                "/api/v5/trade/orders-history", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxOrder>());

        var service = new OkxTradingService(http, symbolMapper, mapper);

        var act = async () => await service.GetOrderHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("100");
    }

    [Fact]
    public async Task Trading_PlaceMarketOrder_SendsMarketOrdTypeAndRefetches()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();

        Dictionary<string, string>? placed = null;
        http.PostAsync<OkxResponse<OkxOrderAck>>(
                "/api/v5/trade/order", Arg.Do<Dictionary<string, string>>(p => placed = p), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxOrderAck> { Data = [new OkxOrderAck { OrdId = "ord-1", SCode = "0" }] });

        http.GetAsync<OkxResponse<OkxOrder>>(
                "/api/v5/trade/order", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxOrder>
            {
                Data = [new OkxOrder { InstId = "BTC-USDT", OrdId = "ord-1", Sz = "0.5", OrdType = "market", State = "filled", Side = "buy" }]
            });

        var service = new OkxTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Market, quantity: 0.5m);
        var order = await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placed.Should().NotBeNull();
        placed!["ordType"].Should().Be("market");
        placed["tdMode"].Should().Be("cash");
        placed.Should().NotContainKey("px"); // market orders carry no price
        order.OrderId.Should().Be("ord-1");
        order.Type.Should().Be(OrderType.Market);
    }

    [Fact]
    public async Task Trading_CancelByClientId_AckOmitsOrderId_RefetchesByClOrdId()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IOkxHttpClient>();

        // The cancel ack omits ordId (only clOrdId is echoed).
        http.PostAsync<OkxResponse<OkxOrderAck>>(
                "/api/v5/trade/cancel-order", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxOrderAck> { Data = [new OkxOrderAck { OrdId = string.Empty, ClOrdId = "cli-77", SCode = "0" }] });

        Dictionary<string, string>? refetchParams = null;
        http.GetAsync<OkxResponse<OkxOrder>>(
                "/api/v5/trade/order", Arg.Do<Dictionary<string, string>>(p => refetchParams = p), true, Arg.Any<CancellationToken>())
            .Returns(new OkxResponse<OkxOrder>
            {
                Data = [new OkxOrder { InstId = "BTC-USDT", OrdId = "real-99", ClOrdId = "cli-77", State = "canceled" }]
            });

        var service = new OkxTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "cli-77", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("real-99");
        order.OrderId.Should().NotBeNullOrEmpty();
        refetchParams.Should().NotBeNull();
        refetchParams!.Should().ContainKey("clOrdId").WhoseValue.Should().Be("cli-77");
        refetchParams.Should().NotContainKey("ordId");
    }

    // ── DI resolution ──

    [Fact]
    public async Task Di_AddOkxExchange_ResolvesKeyedClient()
    {
        var services = new ServiceCollection();
        services.AddOkxExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx);
        client.ExchangeId.Should().Be(ExchangeId.Okx);
    }

    [Fact]
    public async Task Di_AddOkxExchange_Secretless_StillResolvesWorkingClient()
    {
        // A secretless registration must resolve (public market data needs no credentials); the
        // finalizer is a PassThroughHandler rather than a signing handler in this path.
        var services = new ServiceCollection();
        services.AddOkxExchange();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx).ExchangeId.Should().Be(ExchangeId.Okx);
    }

    [Fact]
    public async Task Di_AddOkxExchange_PassphraseMissing_StillResolves()
    {
        // Secret present but passphrase missing: signing is gated OFF (PassThrough), and the client
        // must still resolve so this does NOT trip OkxOptions.ToCredentials() (which throws on empty
        // passphrase). This is the TASK-010 carry-in.
        var services = new ServiceCollection();
        services.AddOkxExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; /* no passphrase */ });
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx).ExchangeId.Should().Be(ExchangeId.Okx);
    }

    [Fact]
    public void Di_AddOkxExchange_BaseUrlWithPath_FailFast()
    {
        // OKX reassembles its signed prehash from RequestUri.AbsolutePath/Query, so a BaseUrl carrying a
        // path segment would break sign-consistency. The shared ExchangeUrl.NormalizeHostRoot guard must
        // fail fast rather than silently produce rejected signatures at runtime.
        var services = new ServiceCollection();
        services.AddOkxExchange(o => o.BaseUrl = "https://www.okx.com/api/v5");
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Di_AddOkxExchange_MapperIsKeyedSingleton()
    {
        var services = new ServiceCollection();
        services.AddOkxExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        using var sp = services.BuildServiceProvider();

        var m1 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Okx);
        var m2 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Okx);
        m1.Should().BeSameAs(m2);
    }

    [Fact]
    public void Di_AddOkxExchange_InvalidOptions_FailFast()
    {
        var services = new ServiceCollection();
        services.AddOkxExchange(o => o.TimeoutSeconds = 0);
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx);
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public async Task Di_AddCryptoExchanges_ResolvesOkxBybitAndBinance()
    {
        var services = new ServiceCollection();
        services.AddCryptoExchanges();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx).ExchangeId.Should().Be(ExchangeId.Okx);
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit).ExchangeId.Should().Be(ExchangeId.Bybit);
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Binance).ExchangeId.Should().Be(ExchangeId.Binance);
    }

    [Fact]
    public async Task Di_AddOkxExchange_IsScopeClean()
    {
        var services = new ServiceCollection();
        services.AddOkxExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        await using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx).Should().NotBeNull();
    }
}
