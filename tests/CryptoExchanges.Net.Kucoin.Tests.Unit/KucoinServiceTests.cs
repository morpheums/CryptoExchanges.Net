using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using NSubstitute;
using DeltaMapper;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Internal;
using CryptoExchanges.Net.Kucoin.Mapping;
using CryptoExchanges.Net.Kucoin.Services;
using CryptoExchanges.Net.Kucoin.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit;

/// <summary>
/// No-network unit tests for the KuCoin services exercised over a mocked
/// <see cref="IKucoinHttpClient"/> (NSubstitute). Covers market data, account, and trading methods,
/// signed-request marking, error-envelope translation, and the public entry point.
/// </summary>
public class KucoinServiceTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (KucoinSymbolMapper symbolMapper, IMapper mapper) BuildMappers(params Symbol[] warmSymbols)
    {
        var symbolMapper = new KucoinSymbolMapper();
        var all = warmSymbols.Length > 0 ? warmSymbols : [BtcUsdt];
        symbolMapper.UpdateSymbols(all.Select(s => new SymbolInfo(s, [OrderType.Limit, OrderType.Market])));
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new KucoinResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return (symbolMapper, config.CreateMapper());
    }

    // ── MapperConfiguration validity ──────────────────────────────────────

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        var act = () => BuildMappers();
        act.Should().NotThrow();
    }

    // ── KucoinMarketDataService ────────────────────────────────────────────

    [Fact]
    public async Task MarketData_GetTickers_SingleSymbol_MapsPayload()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<TickerDto>>(
                "/api/v1/market/stats", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<TickerDto>
            {
                Data = new TickerDto { Symbol = "BTC-USDT", Last = "42000", Open = "40000" }
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var tickers = await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        tickers.Should().HaveCount(1);
        tickers[0].Symbol.Should().Be(BtcUsdt);
        tickers[0].LastPrice.Should().Be(42000m);
    }

    [Fact]
    public async Task MarketData_GetTickers_AllSymbols_FiltersUnresolvable()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<AllTickersDto>>(
                "/api/v1/market/allTickers", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<AllTickersDto>
            {
                Data = new AllTickersDto
                {
                    Ticker =
                    [
                        new TickerDto { Symbol = "BTC-USDT", Last = "42000", Open = "40000", Time = "1700000000000" },
                        new TickerDto { Symbol = "INVALID WIRE", Last = "1", Open = "1", Time = "1700000000000" }
                    ]
                }
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var tickers = await service.GetTickersAsync(null, TestContext.Current.CancellationToken);

        // INVALID WIRE should be skipped without aborting the batch.
        tickers.Should().HaveCount(1);
        tickers[0].Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task MarketData_GetOrderBook_ParsesLevels()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<OrderBookDto>>(
                Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderBookDto>
            {
                Data = new OrderBookDto
                {
                    Bids = [["42000", "2"]],
                    Asks = [["42001", "3"]],
                    Time = 1700000000000L
                }
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var book = await service.GetOrderBookAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);

        book.Symbol.Should().Be(BtcUsdt);
        book.Bids.Should().ContainSingle().Which.Price.Should().Be(42000m);
        book.Asks.Should().ContainSingle().Which.Quantity.Should().Be(3m);
        book.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L));
    }

    [Fact]
    public async Task MarketData_GetOrderBook_LargeDepth_UsesLevel100Endpoint()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        string? capturedEndpoint = null;
        http.GetAsync<ResponseDto<OrderBookDto>>(
                Arg.Do<string>(e => capturedEndpoint = e), Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderBookDto> { Data = new OrderBookDto() });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        await service.GetOrderBookAsync(BtcUsdt, depth: 100, ct: TestContext.Current.CancellationToken);

        capturedEndpoint.Should().Be("/api/v1/market/orderbook/level2_100");
    }

    [Fact]
    public async Task MarketData_GetOrderBook_SmallDepth_UsesLevel20Endpoint()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        string? capturedEndpoint = null;
        http.GetAsync<ResponseDto<OrderBookDto>>(
                Arg.Do<string>(e => capturedEndpoint = e), Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderBookDto> { Data = new OrderBookDto() });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        await service.GetOrderBookAsync(BtcUsdt, depth: 20, ct: TestContext.Current.CancellationToken);

        capturedEndpoint.Should().Be("/api/v1/market/orderbook/level2_20");
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_HappyPath_MapsOhlcvAndOpenTime()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        // KuCoin candles: [startTimeSec, open, close, high, low, vol, quoteVol]
        http.GetAsync<ResponseDto<List<List<string>>>>(
                "/api/v1/market/candles", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<List<string>>>
            {
                Data = [["1700000000", "42000", "42500", "43000", "41000", "10", "420000"]]
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var candles = await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneHour, ct: TestContext.Current.CancellationToken);

        candles.Should().HaveCount(1);
        // KuCoin candle timestamp is unix SECONDS; multiply by 1000 to get ms.
        candles[0].OpenTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000L * 1000L));
        candles[0].Open.Should().Be(42000m);
        candles[0].High.Should().Be(43000m);
        candles[0].Low.Should().Be(41000m);
        candles[0].Close.Should().Be(42500m);
        candles[0].Volume.Should().Be(10m);
        candles[0].QuoteVolume.Should().Be(420000m);
        candles[0].Interval.Should().Be(KlineInterval.OneHour);
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_OneMonthInterval_MapsCorrectly()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<List<List<string>>>>(
                "/api/v1/market/candles", Arg.Do<Dictionary<string, string>>(p => captured = p), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<List<string>>>());

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneMonth, ct: TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!["type"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MarketData_GetCandlesticks_UnsupportedInterval_ThrowsArgumentOutOfRange()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        var service = new KucoinMarketDataService(http, symbolMapper, mapper);

        // Force an out-of-range value not present in KlineInterval to trigger the switch default.
        var badInterval = (KlineInterval)999;
        var act = async () => await service.GetCandlesticksAsync(BtcUsdt, badInterval, ct: TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task MarketData_GetPrice_ReturnsLastPrice()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<TickerDto>>(
                "/api/v1/market/stats", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<TickerDto> { Data = new TickerDto { Symbol = "BTC-USDT", Last = "42500" } });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var price = await service.GetPriceAsync(BtcUsdt, TestContext.Current.CancellationToken);

        price.Should().Be(42500m);
    }

    [Fact]
    public async Task MarketData_GetRecentTrades_MapsTradesFromNsTimestamp()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<List<TradeDto>>>(
                "/api/v1/market/histories", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<TradeDto>>
            {
                Data = [new TradeDto { Sequence = "seq1", Price = "42000", Size = "0.5", Side = "sell", Time = "1700000000000000000" }]
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var trades = await service.GetRecentTradesAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);

        trades.Should().HaveCount(1);
        trades[0].Symbol.Should().Be(BtcUsdt);
        trades[0].Price.Should().Be(42000m);
        trades[0].Quantity.Should().Be(0.5m);
        // IsBuyerMaker = (side == "sell"), so true when taker is seller.
        trades[0].IsBuyerMaker.Should().BeTrue();
        trades[0].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L));
    }

    [Fact]
    public async Task MarketData_GetExchangeInfo_PopulatesSymbolMapper()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<List<SymbolInfoDto>>>(
                "/api/v2/symbols", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<SymbolInfoDto>>
            {
                Data = [new SymbolInfoDto { Symbol = "BTC-USDT", BaseCurrency = "BTC", QuoteCurrency = "USDT" }]
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var info = await service.GetExchangeInfoAsync(TestContext.Current.CancellationToken);

        info.ExchangeName.Should().Be("KuCoin");
        info.Symbols.Should().HaveCount(1);
        info.Symbols[0].Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task MarketData_GetExchangeInfo_RejectsInvalidCurrencies()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<List<SymbolInfoDto>>>(
                "/api/v2/symbols", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<SymbolInfoDto>>
            {
                Data =
                [
                    new SymbolInfoDto { Symbol = "BTC-USDT", BaseCurrency = "BTC", QuoteCurrency = "USDT" },
                    new SymbolInfoDto { Symbol = "INVALID!-USDT", BaseCurrency = "INVALID!@#", QuoteCurrency = "USDT" }
                ]
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var info = await service.GetExchangeInfoAsync(TestContext.Current.CancellationToken);

        info.Symbols.Should().HaveCount(1);
    }

    [Fact]
    public async Task MarketData_IsSupportedAsync_AfterExchangeInfo_ReturnsTrue()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<List<SymbolInfoDto>>>(
                "/api/v2/symbols", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<SymbolInfoDto>>
            {
                Data = [new SymbolInfoDto { Symbol = "BTC-USDT", BaseCurrency = "BTC", QuoteCurrency = "USDT" }]
            });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        // Warm the cache via GetExchangeInfoAsync first.
        await service.GetExchangeInfoAsync(TestContext.Current.CancellationToken);
        var supported = await service.IsSupportedAsync(BtcUsdt, TestContext.Current.CancellationToken);

        supported.Should().BeTrue();
    }

    // ── Signed request marking ─────────────────────────────────────────────

    [Fact]
    public async Task Account_GetBalances_EmitsSignedRequest()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        bool signedCaptured = false;
        http.GetAsync<ResponseDto<List<BalanceDto>>>(
                "/api/v1/accounts", Arg.Any<Dictionary<string, string>>(),
                Arg.Do<bool>(s => signedCaptured = s), Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<BalanceDto>> { Data = [] });

        var service = new KucoinAccountService(http, symbolMapper, mapper);
        await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        signedCaptured.Should().BeTrue("balances endpoint is authenticated");
    }

    // ── KucoinAccountService ───────────────────────────────────────────────

    [Fact]
    public async Task Account_GetBalances_TrimsZeroBalances()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<List<BalanceDto>>>(
                "/api/v1/accounts", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<BalanceDto>>
            {
                Data =
                [
                    new BalanceDto { Currency = "BTC", Available = "1.5", Holds = "0" },
                    new BalanceDto { Currency = "ETH", Available = "0", Holds = "0" }
                ]
            });

        var service = new KucoinAccountService(http, symbolMapper, mapper);
        var balances = await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        balances.Should().HaveCount(1);
        balances[0].Asset.Should().Be(Asset.Btc);
        balances[0].Total.Should().Be(1.5m);
    }

    [Fact]
    public async Task Account_GetBalance_ByAsset_ReturnsMatch()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<List<BalanceDto>>>(
                "/api/v1/accounts", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<BalanceDto>>
            {
                Data = [new BalanceDto { Currency = "BTC", Available = "2.5", Holds = "0.5" }]
            });

        var service = new KucoinAccountService(http, symbolMapper, mapper);
        var balance = await service.GetBalanceAsync(Asset.Btc, TestContext.Current.CancellationToken);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(2.5m);
        balance.Locked.Should().Be(0.5m);
        balance.Total.Should().Be(3m);
    }

    [Fact]
    public async Task Account_GetBalance_MissingAsset_ReturnsZeroBalance()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<List<BalanceDto>>>(
                "/api/v1/accounts", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<BalanceDto>> { Data = [] });

        var service = new KucoinAccountService(http, symbolMapper, mapper);
        var balance = await service.GetBalanceAsync(Asset.Eth, TestContext.Current.CancellationToken);

        balance.Asset.Should().Be(Asset.Eth);
        balance.Free.Should().Be(0m);
        balance.Locked.Should().Be(0m);
    }

    [Fact]
    public async Task Account_GetTradeHistory_IsSignedAndMapsViaFillDto()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        bool signedCaptured = false;
        http.GetAsync<ResponseDto<ListDto<FillDto>>>(
                "/api/v1/fills", Arg.Any<Dictionary<string, string>>(),
                Arg.Do<bool>(s => signedCaptured = s), Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<FillDto>>
            {
                Data = new ListDto<FillDto>
                {
                    Items =
                    [
                        new FillDto { TradeId = "t1", OrderId = "o1", Symbol = "BTC-USDT",
                            Price = "42000", Size = "0.1", Side = "buy", Liquidity = "maker",
                            CreatedAt = 1700000000000L }
                    ]
                }
            });

        var service = new KucoinAccountService(http, symbolMapper, mapper);
        var trades = await service.GetTradeHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);

        signedCaptured.Should().BeTrue("fills endpoint is authenticated");
        trades.Should().HaveCount(1);
        trades[0].Symbol.Should().Be(BtcUsdt);
        trades[0].Id.Should().Be("t1");
        trades[0].Price.Should().Be(42000m);
        // buy + maker: IsBuyerMaker = true
        trades[0].IsBuyerMaker.Should().BeTrue();
    }

    [Fact]
    public async Task Account_GetTradeHistory_LimitClamped()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<ListDto<FillDto>>>(
                "/api/v1/fills", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<FillDto>>());

        var service = new KucoinAccountService(http, symbolMapper, mapper);
        // Default is 500, which is the max. Pass 1000 to verify clamping.
        await service.GetTradeHistoryAsync(BtcUsdt, limit: 1000, ct: TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!["pageSize"].Should().Be("500");
    }

    // ── KucoinTradingService ───────────────────────────────────────────────

    [Fact]
    public async Task Trading_GetOpenOrders_IsSignedAndMapsOrders()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        bool signedCaptured = false;
        http.GetAsync<ResponseDto<ListDto<OrderDto>>>(
                "/api/v1/orders", Arg.Any<Dictionary<string, string>>(),
                Arg.Do<bool>(s => signedCaptured = s), Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<OrderDto>>
            {
                Data = new ListDto<OrderDto>
                {
                    Items =
                    [
                        new OrderDto { Id = "ord-1", Symbol = "BTC-USDT", Price = "42000",
                            Size = "1", Side = "buy", Type = "limit", IsActive = true,
                            TimeInForce = "GTC", CreatedAt = 1700000000000L }
                    ]
                }
            });

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOpenOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        signedCaptured.Should().BeTrue("open orders endpoint is authenticated");
        orders.Should().HaveCount(1);
        orders[0].OrderId.Should().Be("ord-1");
        orders[0].Symbol.Should().Be(BtcUsdt);
        orders[0].Status.Should().Be(OrderStatus.New);
    }

    [Fact]
    public async Task Trading_GetOrderHistory_LimitClamped()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<ListDto<OrderDto>>>(
                "/api/v1/orders", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<OrderDto>>());

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        await service.GetOrderHistoryAsync(BtcUsdt, limit: 1000, ct: TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!["pageSize"].Should().Be("500");
    }

    [Fact]
    public async Task Trading_PlaceOrder_IsSignedAndBuildsCorrectBody()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();

        Dictionary<string, string>? placedParams = null;
        http.PostAsync<ResponseDto<OrderAckDto>>(
                "/api/v1/orders", Arg.Do<Dictionary<string, string>>(p => placedParams = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderAckDto> { Data = new OrderAckDto { OrderId = "new-ord-1" } });

        // Re-fetch order after place
        http.GetAsync<ResponseDto<OrderDto>>(
                Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = new OrderDto { Id = "new-ord-1", Symbol = "BTC-USDT", Price = "42000",
                    Size = "0.5", Side = "buy", Type = "limit", IsActive = true,
                    TimeInForce = "GTC", CreatedAt = 1700000000000L }
            });

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Limit, quantity: 0.5m, price: 42000m);
        var order = await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placedParams.Should().NotBeNull();
        placedParams!["symbol"].Should().Be("BTC-USDT");
        placedParams["side"].Should().Be("buy");
        placedParams["type"].Should().Be("limit");
        placedParams["size"].Should().Be("0.5");
        placedParams["price"].Should().Be("42000");
        order.OrderId.Should().Be("new-ord-1");
        order.Type.Should().Be(OrderType.Limit);
    }

    [Fact]
    public async Task Trading_PlaceMarketOrder_NoPrice()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();

        Dictionary<string, string>? placedParams = null;
        http.PostAsync<ResponseDto<OrderAckDto>>(
                "/api/v1/orders", Arg.Do<Dictionary<string, string>>(p => placedParams = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderAckDto> { Data = new OrderAckDto { OrderId = "mkt-1" } });

        http.GetAsync<ResponseDto<OrderDto>>(
                Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = new OrderDto { Id = "mkt-1", Symbol = "BTC-USDT", Side = "buy",
                    Type = "market", IsActive = false, TimeInForce = "IOC", CreatedAt = 1700000000000L }
            });

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Market, quantity: 0.5m);
        var order = await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placedParams.Should().NotBeNull();
        placedParams!["type"].Should().Be("market");
        placedParams.Should().NotContainKey("price"); // market orders must not carry price
        order.OrderId.Should().Be("mkt-1");
    }

    [Fact]
    public async Task Trading_CancelOrder_IsSignedAndRefetches()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        bool signedCaptured = false;
        http.DeleteAsync<ResponseDto<CancelOrderAckDto>>(
                "/api/v1/orders/ord-99", Arg.Any<Dictionary<string, string>>(),
                Arg.Do<bool>(s => signedCaptured = s), Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<CancelOrderAckDto>
            {
                Data = new CancelOrderAckDto { CancelledOrderIds = ["ord-99"] }
            });

        http.GetAsync<ResponseDto<OrderDto>>(
                "/api/v1/orders/ord-99", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = new OrderDto { Id = "ord-99", Symbol = "BTC-USDT", Side = "sell",
                    Type = "limit", IsActive = false, CancelExist = true,
                    TimeInForce = "GTC", CreatedAt = 1700000000000L }
            });

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderAsync(BtcUsdt, "ord-99", TestContext.Current.CancellationToken);

        signedCaptured.Should().BeTrue("cancel endpoint is authenticated");
        order.OrderId.Should().Be("ord-99");
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public async Task Trading_CancelOrderByClientId_RefetchesByClientId()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();

        http.DeleteAsync<ResponseDto<CancelOrderAckDto>>(
                "/api/v1/order/client-order/cli-77", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<CancelOrderAckDto>
            {
                Data = new CancelOrderAckDto { CancelledOrderIds = [] }
            });

        // Empty cancelledOrderIds means empty orderId; should fall back to clientOrderId refetch.
        http.GetAsync<ResponseDto<OrderDto>>(
                "/api/v1/order/client-order/cli-77", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = new OrderDto { Id = "real-99", Symbol = "BTC-USDT", Side = "sell",
                    Type = "limit", IsActive = false, CancelExist = true,
                    TimeInForce = "GTC", CreatedAt = 1700000000000L }
            });

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "cli-77", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("real-99");
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public async Task Trading_CancelAllOrders_IsSignedAndRefetchesFromHistory()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();

        http.DeleteAsync<ResponseDto<CancelOrderAckDto>>(
                "/api/v1/orders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<CancelOrderAckDto>
            {
                Data = new CancelOrderAckDto { CancelledOrderIds = ["ord-1", "ord-2"] }
            });

        // GetOrderHistoryAsync call.
        http.GetAsync<ResponseDto<ListDto<OrderDto>>>(
                "/api/v1/orders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<OrderDto>>
            {
                Data = new ListDto<OrderDto>
                {
                    Items =
                    [
                        new OrderDto { Id = "ord-1", Symbol = "BTC-USDT", Side = "sell", Type = "limit",
                            IsActive = false, CancelExist = true, TimeInForce = "GTC", CreatedAt = 1700000000000L },
                        new OrderDto { Id = "ord-2", Symbol = "BTC-USDT", Side = "buy", Type = "limit",
                            IsActive = false, CancelExist = true, TimeInForce = "GTC", CreatedAt = 1700000000000L },
                        new OrderDto { Id = "ord-3", Symbol = "BTC-USDT", Side = "buy", Type = "limit",
                            IsActive = false, CancelExist = false, TimeInForce = "GTC", CreatedAt = 1700000000000L }
                    ]
                }
            });

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var cancelled = await service.CancelAllOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        // Only ord-1 and ord-2 were in the cancel ack; ord-3 should be excluded.
        cancelled.Should().HaveCount(2);
        cancelled.Select(o => o.OrderId).Should().BeEquivalentTo(["ord-1", "ord-2"]);
    }

    [Fact]
    public async Task Trading_GetOrder_IsSignedAndMapsOrder()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        bool signedCaptured = false;
        http.GetAsync<ResponseDto<OrderDto>>(
                "/api/v1/orders/ord-42", Arg.Any<Dictionary<string, string>>(),
                Arg.Do<bool>(s => signedCaptured = s), Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderDto>
            {
                Data = new OrderDto { Id = "ord-42", Symbol = "BTC-USDT", Price = "42000",
                    Size = "1", Side = "buy", Type = "limit", IsActive = true,
                    TimeInForce = "GTC", CreatedAt = 1700000000000L }
            });

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var order = await service.GetOrderAsync(BtcUsdt, "ord-42", TestContext.Current.CancellationToken);

        signedCaptured.Should().BeTrue("get order endpoint is authenticated");
        order.OrderId.Should().Be("ord-42");
        order.Status.Should().Be(OrderStatus.New);
    }

    // ── Error envelope translation ─────────────────────────────────────────

    [Fact]
    public async Task MarketData_GetTickers_ErrorEnvelope_SurfacesExchangeException()
    {
        // The error translator fires at the resilience pipeline level (HTTP handler); this test
        // verifies that the service properly propagates the typed exception when the http client
        // throws (as it would for a non-200000 code after the pipeline translates it).
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<TickerDto>>(
                Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<ResponseDto<TickerDto>>(_ => throw new ExchangeApiException("KuCoin error 400001: timestamp expired", 400001, "{}"));

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        var act = async () => await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ExchangeApiException>()
            .WithMessage("*timestamp expired*");
    }

    [Fact]
    public async Task Trading_PlaceOrder_ErrorEnvelope_SurfacesInvalidOrderException()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.PostAsync<ResponseDto<OrderAckDto>>(
                Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<ResponseDto<OrderAckDto>>(_ => throw new InvalidOrderException("KuCoin error 900002: invalid price", 900002, "{}"));

        var service = new KucoinTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Limit, quantity: 0.5m, price: 42000m);
        var act = async () => await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOrderException>();
    }

    // ── KucoinExchangeClient entry point ──────────────────────────────────

    [Fact]
    public void ExchangeClient_Create_ReturnsKucoinClient()
    {
        var client = KucoinExchangeClient.Create(new KucoinOptions());
        client.ExchangeId.Should().Be(ExchangeId.Kucoin);
        client.MarketData.Should().NotBeNull();
        client.Trading.Should().NotBeNull();
        client.Account.Should().NotBeNull();
    }

    [Fact]
    public void ExchangeClient_Create_WithCredentials_ReturnsKucoinClient()
    {
        var client = KucoinExchangeClient.Create(new KucoinOptions
        {
            ApiKey = "test-key",
            SecretKey = "test-secret",
            Passphrase = "test-passphrase"
        });
        client.ExchangeId.Should().Be(ExchangeId.Kucoin);
    }

    [Fact]
    public void ClientComposer_BuildResilientHttpClient_ZeroLengthOffsetHolder_ThrowsArgumentException()
    {
        // LR-004: Both null AND zero-length array guards are required before indexed access.
        var act = () => KucoinClientComposer.BuildResilientHttpClient(new KucoinOptions(), Array.Empty<long>());
        act.Should().Throw<ArgumentException>().WithMessage("*offsetHolder*");
    }

    [Fact]
    public async Task ExchangeClient_PingAsync_ReturnsFalseOnException()
    {
        // Verify PingAsync returns false on exchange exceptions (not rethrow).
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<long>>(
                "/api/v1/timestamp", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns<ResponseDto<long>>(_ => throw new ExchangeApiException("server error", null, "{}"));

        var timeSync = Substitute.For<Core.Resilience.IExchangeTimeSync>();
        var market = new KucoinMarketDataService(http, symbolMapper, mapper);
        var trading = new KucoinTradingService(http, symbolMapper, mapper);
        var account = new KucoinAccountService(http, symbolMapper, mapper);
        var client = new KucoinExchangeClient(http, market, trading, account, false, null, [0L], timeSync);

        var result = await client.PingAsync(TestContext.Current.CancellationToken);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExchangeClient_SyncServerTimeAsync_UpdatesOffset()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        http.GetAsync<ResponseDto<long>>(
                "/api/v1/timestamp", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<long> { Data = 1700000000000L });

        long[]? capturedHolder = null;
        var timeSync = Substitute.For<Core.Resilience.IExchangeTimeSync>();
        timeSync.When(t => t.ApplyOffset(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long[]>()))
            .Do(ci => capturedHolder = ci.ArgAt<long[]>(2));

        var market = new KucoinMarketDataService(http, symbolMapper, mapper);
        var trading = new KucoinTradingService(http, symbolMapper, mapper);
        var account = new KucoinAccountService(http, symbolMapper, mapper);
        var holder = new long[] { 0L };
        var client = new KucoinExchangeClient(http, market, trading, account, false, null, holder, timeSync);

        await client.SyncServerTimeAsync(TestContext.Current.CancellationToken);

        timeSync.Received(1).ApplyOffset(1700000000000L, Arg.Any<long>(), holder);
    }

    // ── KucoinRequestValidation ────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void RequestValidation_InvalidLimit_ThrowsArgumentOutOfRange(int limit)
    {
        var act = () => KucoinRequestValidation.ValidateHistoryWindow(limit, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RequestValidation_StartAfterEnd_ThrowsArgumentException()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(-1);
        var act = () => KucoinRequestValidation.ValidateHistoryWindow(100, start, end);
        act.Should().Throw<ArgumentException>().WithMessage("*startTime*");
    }

    [Fact]
    public void RequestValidation_ValidWindow_DoesNotThrow()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-7);
        var end = DateTimeOffset.UtcNow;
        var act = () => KucoinRequestValidation.ValidateHistoryWindow(100, start, end);
        act.Should().NotThrow();
    }

    // ── GET retry / signed re-sign behavior  ──────────────────────────────
    // These tests exercise the signing request marker via a stub HttpMessageHandler
    // that asserts the KC-API-SIGN header is present on each attempt.

    [Fact]
    public async Task SigningRequest_SignedGet_MarksSigned()
    {
        // Verifies that the service marks GET requests with KucoinSigningRequest when signed=true.
        // Uses the NSubstitute mock which captures the 'signed' bool parameter.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        bool signedArg = false;
        http.GetAsync<ResponseDto<List<BalanceDto>>>(
                Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Do<bool>(s => signedArg = s), Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<List<BalanceDto>> { Data = [] });

        var service = new KucoinAccountService(http, symbolMapper, mapper);
        await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        signedArg.Should().BeTrue("GET /api/v1/accounts is a private endpoint requiring signing");
    }

    [Fact]
    public async Task SigningRequest_PublicGet_NotSigned()
    {
        // Public market-data endpoints must NOT be signed.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKucoinHttpClient>();
        bool signedArg = true; // default to true to ensure it's overwritten
        http.GetAsync<ResponseDto<TickerDto>>(
                Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Do<bool>(s => signedArg = s), Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<TickerDto> { Data = new TickerDto { Symbol = "BTC-USDT" } });

        var service = new KucoinMarketDataService(http, symbolMapper, mapper);
        await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        signedArg.Should().BeFalse("public ticker endpoint must not be signed");
    }
}
