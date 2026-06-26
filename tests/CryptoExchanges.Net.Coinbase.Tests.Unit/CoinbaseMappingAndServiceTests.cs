using System.Net;
using System.Net.Http;
using Xunit;
using AwesomeAssertions;
using NSubstitute;
using DeltaMapper;
using CryptoExchanges.Net.Coinbase;
using CryptoExchanges.Net.Coinbase.Dtos;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Coinbase.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Coinbase.Tests.Unit;

/// <summary>No-network unit tests for Coinbase DTO mapping and service round-trips via NSubstitute.</summary>
public class CoinbaseMappingAndServiceTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (ISymbolMapper symbolMapper, IMapper mapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(CoinbaseSymbolFormat.Instance);
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit, OrderType.Market])]);
        var mapper = CoinbaseClientComposer.CreateMapper(symbolMapper);
        return (symbolMapper, mapper);
    }

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        var act = () => BuildMappers();
        act.Should().NotThrow();
    }

    [Fact]
    public void OrderProfile_LimitGtc_MapsAllScalars()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OrderDto
        {
            OrderId = "ord-1",
            ClientOrderId = "cli-1",
            ProductId = "BTC-USDT",
            Side = "BUY",
            Status = "OPEN",
            FilledSize = "0.5",
            FilledValue = "20000",
            CreatedTime = "2024-01-01T00:00:00Z",
            OrderConfiguration = new OrderConfigurationDto
            {
                LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "40000", PostOnly = false }
            }
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.Symbol.Should().Be(BtcUsdt);
        order.OrderId.Should().Be("ord-1");
        order.ClientOrderId.Should().Be("cli-1");
        order.Price.Should().Be(40000m);
        order.OriginalQuantity.Should().Be(1m);
        order.ExecutedQuantity.Should().Be(0.5m);
        order.CumulativeQuoteQuantity.Should().Be(20000m);
        order.Side.Should().Be(OrderSide.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.Status.Should().Be(OrderStatus.New);
        order.TimeInForce.Should().Be(TimeInForce.Gtc);
        order.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public void OrderProfile_MarketIoc_MapsCleanly()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OrderDto
        {
            OrderId = "ord-2",
            ProductId = "BTC-USDT",
            Side = "SELL",
            Status = "FILLED",
            FilledSize = "0.1",
            FilledValue = "4000",
            OrderConfiguration = new OrderConfigurationDto
            {
                MarketIoc = new MarketIocDto { BaseSize = "0.1", QuoteSize = "0" }
            }
        };

        var act = () => mapper.Map<OrderDto, Order>(dto);
        act.Should().NotThrow();

        var order = mapper.Map<OrderDto, Order>(dto);
        order.Type.Should().Be(OrderType.Market);
        order.TimeInForce.Should().Be(TimeInForce.Ioc);
        order.Status.Should().Be(OrderStatus.Filled);
        order.Side.Should().Be(OrderSide.Sell);
    }

    [Fact]
    public void TickerProfile_MapsAllFields()
    {
        var (_, mapper) = BuildMappers();
        var dto = new TickerDto
        {
            ProductId = "BTC-USDT",
            Price = "42000",
            High24h = "43000",
            Low24h = "40000",
            Volume24h = "100",
            Volume24hUsd = "4200000",
            PricePercentChg24h = "5",
            Time = "2024-01-01T00:00:00Z"
        };

        var ticker = mapper.Map<TickerDto, Ticker>(dto);

        ticker.Symbol.Should().Be(BtcUsdt);
        ticker.LastPrice.Should().Be(42000m);
        ticker.HighPrice.Should().Be(43000m);
        ticker.LowPrice.Should().Be(40000m);
        ticker.Volume.Should().Be(100m);
        ticker.QuoteVolume.Should().Be(4200000m);
        ticker.PriceChangePercent.Should().Be(5m);
        ticker.Timestamp.Should().NotBeNull();
    }

    [Fact]
    public void AccountProfile_MapsAvailableAndHold()
    {
        var (_, mapper) = BuildMappers();
        var dto = new AccountDto
        {
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
    public void FillProfile_MapsFillDtoToTrade()
    {
        var (_, mapper) = BuildMappers();
        var dto = new FillDto
        {
            TradeId = "t-1",
            OrderId = "o-1",
            ProductId = "BTC-USDT",
            Price = "42000",
            Size = "0.5",
            Side = "SELL",
            LiquidityIndicator = "MAKER",
            TradeTime = "2024-01-01T00:00:00Z"
        };

        var trade = mapper.Map<FillDto, Trade>(dto);

        trade.Id.Should().Be("t-1");
        trade.OrderId.Should().Be("o-1");
        trade.Price.Should().Be(42000m);
        trade.Quantity.Should().Be(0.5m);
        trade.IsBuyerMaker.Should().BeTrue(); // SELL taker means buyer was maker
        trade.Timestamp.Should().NotBeNull();
    }

    [Fact]
    public async Task MarketData_GetTickers_SingleSymbol_MapsPayload()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();
        http.GetAsync<TickerDto>(
                Arg.Is<string>(s => s.Contains("BTC-USDT")), null, false, Arg.Any<CancellationToken>())
            .Returns(new TickerDto { ProductId = "BTC-USDT", Price = "42000" });

        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);
        var tickers = await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        tickers.Should().HaveCount(1);
        tickers[0].Symbol.Should().Be(BtcUsdt);
        tickers[0].LastPrice.Should().Be(42000m);
    }

    [Fact]
    public async Task Account_GetBalances_TrimsZeroBalances()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();
        http.GetPropertyAsync<List<AccountDto>>(
                "/api/v3/brokerage/accounts", "accounts", null, true, Arg.Any<CancellationToken>())
            .Returns(new List<AccountDto>
            {
                new() { Currency = "BTC", AvailableBalance = new AmountDto { Value = "1.5" }, Hold = new AmountDto { Value = "0" } },
                new() { Currency = "ETH", AvailableBalance = new AmountDto { Value = "0" }, Hold = new AmountDto { Value = "0" } }
            });

        var service = new CoinbaseAccountService(http, symbolMapper, mapper);
        var balances = await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        balances.Should().HaveCount(1);
        balances[0].Asset.Should().Be(Asset.Btc);
        balances[0].Total.Should().Be(1.5m);
    }

    [Fact]
    public async Task Account_GetTradeHistory_DefaultLimit_ClampsToHundred()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetPropertyAsync<List<FillDto>>(
                "/api/v3/brokerage/orders/historical/fills",
                "fills",
                Arg.Do<Dictionary<string, string>>(p => captured = p),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new List<FillDto>());

        var service = new CoinbaseAccountService(http, symbolMapper, mapper);

        // Default IExchangeClient limit is 500; the service must clamp to 100, not throw.
        var act = async () => await service.GetTradeHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("100");
    }

    [Fact]
    public async Task Trading_GetOpenOrders_MapsOrders()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();
        http.GetPropertyAsync<List<OrderDto>>(
                "/api/v3/brokerage/orders/historical/batch",
                "orders",
                Arg.Any<Dictionary<string, string>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new List<OrderDto>
            {
                new()
                {
                    OrderId = "ord-9",
                    ProductId = "BTC-USDT",
                    Side = "BUY",
                    Status = "OPEN",
                    OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "5" } }
                }
            });

        var service = new CoinbaseTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOpenOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        orders.Should().HaveCount(1);
        orders[0].OrderId.Should().Be("ord-9");
        orders[0].Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task Trading_PlaceLimitOrder_SendsBody_AndRefetches()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();

        object? capturedBody = null;
        http.PostAsync<PlaceOrderAckDto>(
                "/api/v3/brokerage/orders",
                Arg.Do<object>(b => capturedBody = b),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new PlaceOrderAckDto
            {
                Success = true,
                SuccessResponse = new PlaceOrderSuccessDto { OrderId = "ord-placed" }
            });

        http.GetPropertyAsync<OrderDto?>(
                Arg.Is<string>(s => s.Contains("ord-placed")),
                "order",
                null,
                true,
                Arg.Any<CancellationToken>())
            .Returns(new OrderDto
            {
                OrderId = "ord-placed",
                ProductId = "BTC-USDT",
                Side = "BUY",
                Status = "OPEN",
                OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "40000" } }
            });

        var service = new CoinbaseTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Limit, price: 40000m, quantity: 1m);
        var order = await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        capturedBody.Should().NotBeNull();
        order.OrderId.Should().Be("ord-placed");
        order.Type.Should().Be(OrderType.Limit);
    }

    [Fact]
    public async Task Trading_PlaceOrderFails_ThrowsInvalidOrderException()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();

        http.PostAsync<PlaceOrderAckDto>(
                "/api/v3/brokerage/orders",
                Arg.Any<object>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new PlaceOrderAckDto
            {
                Success = false,
                ErrorResponse = new PlaceOrderRejectionDto { Error = "INSUFFICIENT_FUNDS", Message = "Not enough funds" }
            });

        var service = new CoinbaseTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Market, quantity: 100m);
        var act = async () => await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<CryptoExchanges.Net.Core.Exceptions.InvalidOrderException>();
    }

    [Fact]
    public async Task SignedRequest_MarksRequest_AuthorizationHeaderInjectedByHandler()
    {
        var captured = new List<bool>();
        var inner = new CapturingHandler(r =>
        {
            // JwtInjectingHandler has already set Authorization before this inner handler fires.
            captured.Add(r.Headers.TryGetValues("Authorization", out _));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

        var jwtInjector = new JwtInjectingHandler(inner);
        var httpClient = new HttpClient(jwtInjector) { BaseAddress = new Uri("https://api.coinbase.com") };
        var coinbaseHttp = new CoinbaseHttpClient(httpClient);

        // signed=true causes CoinbaseSigningRequest.MarkSigned; JwtInjectingHandler adds Bearer.
        await coinbaseHttp.GetAsync<object>("/api/v3/brokerage/accounts", signed: true);

        captured.Should().HaveCount(1);
        captured[0].Should().BeTrue();
    }

    [Fact]
    public async Task UnsignedRequest_NoCredentials_NoAuthorizationHeader()
    {
        var captured = new List<bool>();
        var inner = new CapturingHandler(r =>
        {
            captured.Add(r.Headers.TryGetValues("Authorization", out _));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

        var passThrough = new Http.PassThroughHandler { InnerHandler = inner };
        var httpClient = new HttpClient(passThrough) { BaseAddress = new Uri("https://api.coinbase.com") };
        var coinbaseHttp = new CoinbaseHttpClient(httpClient);

        await coinbaseHttp.GetAsync<object>("/api/v3/brokerage/products", signed: false);

        captured.Should().HaveCount(1);
        captured[0].Should().BeFalse();
    }

    [Fact]
    public async Task Account_GetBalance_SingleAsset_ReturnsMappedBalance()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();
        http.GetPropertyAsync<List<AccountDto>>(
                "/api/v3/brokerage/accounts", "accounts", null, true, Arg.Any<CancellationToken>())
            .Returns(new List<AccountDto>
            {
                new() { Currency = "BTC", AvailableBalance = new AmountDto { Value = "2.5" }, Hold = new AmountDto { Value = "0.5" } },
                new() { Currency = "ETH", AvailableBalance = new AmountDto { Value = "10" }, Hold = new AmountDto { Value = "0" } }
            });

        var service = new CoinbaseAccountService(http, symbolMapper, mapper);
        var balance = await service.GetBalanceAsync(Asset.Btc, TestContext.Current.CancellationToken);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(2.5m);
        balance.Locked.Should().Be(0.5m);
        balance.Total.Should().Be(3m);
    }

    [Fact]
    public async Task Trading_CancelOrder_PostsBatchCancel_AndRefetchesOrder()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();

        http.PostPropertyAsync<List<CancelOrderEntryDto>>(
                "/api/v3/brokerage/orders/batch_cancel",
                "results",
                Arg.Any<object>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new List<CancelOrderEntryDto> { new() { OrderId = "ord-cancel", Success = true } });

        http.GetPropertyAsync<OrderDto?>(
                Arg.Is<string>(s => s.Contains("ord-cancel")),
                "order",
                null,
                true,
                Arg.Any<CancellationToken>())
            .Returns(new OrderDto
            {
                OrderId = "ord-cancel",
                ProductId = "BTC-USDT",
                Side = "SELL",
                Status = "CANCELLED",
                OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "30000" } }
            });

        var service = new CoinbaseTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderAsync(BtcUsdt, "ord-cancel", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("ord-cancel");
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public async Task Trading_CancelOrderByClientId_FoundOpenOrder_CancelsAndRefetches()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();

        // Mock GetOpenOrdersAsync (used to find the exchange order_id for the client_order_id).
        http.GetPropertyAsync<List<OrderDto>>(
                "/api/v3/brokerage/orders/historical/batch",
                "orders",
                Arg.Any<Dictionary<string, string>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new List<OrderDto>
            {
                new()
                {
                    OrderId = "ord-bycli",
                    ClientOrderId = "cli-abc",
                    ProductId = "BTC-USDT",
                    Side = "BUY",
                    Status = "OPEN",
                    OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "40000" } }
                }
            });

        http.PostPropertyAsync<List<CancelOrderEntryDto>>(
                "/api/v3/brokerage/orders/batch_cancel",
                "results",
                Arg.Any<object>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new List<CancelOrderEntryDto> { new() { OrderId = "ord-bycli", Success = true } });

        http.GetPropertyAsync<OrderDto?>(
                Arg.Is<string>(s => s.Contains("ord-bycli")),
                "order",
                null,
                true,
                Arg.Any<CancellationToken>())
            .Returns(new OrderDto
            {
                OrderId = "ord-bycli",
                ClientOrderId = "cli-abc",
                ProductId = "BTC-USDT",
                Side = "BUY",
                Status = "CANCELLED",
                OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "40000" } }
            });

        var service = new CoinbaseTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "cli-abc", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("ord-bycli");
        order.ClientOrderId.Should().Be("cli-abc");
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public async Task Trading_CancelAllOrders_BatchCancelsAndReturnsSucceeded()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();

        http.GetPropertyAsync<List<OrderDto>>(
                "/api/v3/brokerage/orders/historical/batch",
                "orders",
                Arg.Any<Dictionary<string, string>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new List<OrderDto>
            {
                new()
                {
                    OrderId = "ord-all-1",
                    ProductId = "BTC-USDT",
                    Side = "BUY",
                    Status = "OPEN",
                    OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "40000" } }
                },
                new()
                {
                    OrderId = "ord-all-2",
                    ProductId = "BTC-USDT",
                    Side = "SELL",
                    Status = "CANCELLED",
                    OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "0.5", LimitPrice = "41000" } }
                }
            });

        http.PostPropertyAsync<List<CancelOrderEntryDto>>(
                "/api/v3/brokerage/orders/batch_cancel",
                "results",
                Arg.Any<object>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new List<CancelOrderEntryDto>
            {
                new() { OrderId = "ord-all-1", Success = true },
                new() { OrderId = "ord-all-2", Success = false }
            });

        var service = new CoinbaseTradingService(http, symbolMapper, mapper);
        var canceled = await service.CancelAllOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        // Only the successfully canceled order should be returned.
        canceled.Should().HaveCount(1);
        canceled[0].OrderId.Should().Be("ord-all-1");
    }

    [Fact]
    public async Task Trading_GetOrder_FetchesAndMapsOrder()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<ICoinbaseHttpClient>();

        http.GetPropertyAsync<OrderDto?>(
                Arg.Is<string>(s => s.Contains("ord-fetch")),
                "order",
                null,
                true,
                Arg.Any<CancellationToken>())
            .Returns(new OrderDto
            {
                OrderId = "ord-fetch",
                ClientOrderId = "cli-fetch",
                ProductId = "BTC-USDT",
                Side = "BUY",
                Status = "FILLED",
                FilledSize = "1",
                FilledValue = "40000",
                OrderConfiguration = new OrderConfigurationDto { LimitGtc = new LimitGtcDto { BaseSize = "1", LimitPrice = "40000" } }
            });

        var service = new CoinbaseTradingService(http, symbolMapper, mapper);
        var order = await service.GetOrderAsync(BtcUsdt, "ord-fetch", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("ord-fetch");
        order.ClientOrderId.Should().Be("cli-fetch");
        order.Symbol.Should().Be(BtcUsdt);
        order.Status.Should().Be(OrderStatus.Filled);
        order.Side.Should().Be(OrderSide.Buy);
    }

    // These tests drive the real CoinbaseHttpClient through a fake handler returning the full
    // named-property envelope JSON, exercising GetPropertyAsync/PostPropertyAsync extraction end-to-end.

    [Fact]
    public async Task GetPropertyAsync_ExtractsAccountsEnvelope_MapsBalances()
    {
        var (symbolMapper, mapper) = BuildMappers();
        const string json = """
            {"accounts":[
                {"currency":"BTC","available_balance":{"value":"1.5","currency":"BTC"},"hold":{"value":"0.25","currency":"BTC"}},
                {"currency":"ETH","available_balance":{"value":"0","currency":"ETH"},"hold":{"value":"0","currency":"ETH"}}
            ],"has_next":false,"cursor":"page-2"}
            """;
        var coinbaseHttp = BuildRealClient(_ => json);
        var service = new CoinbaseAccountService(coinbaseHttp, symbolMapper, mapper);

        var balances = await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        balances.Should().ContainSingle();
        balances[0].Asset.Should().Be(Asset.Btc);
        balances[0].Total.Should().Be(1.75m);
    }

    [Fact]
    public async Task GetPropertyAsync_ExtractsPricebookEnvelope_MapsOrderBook()
    {
        var (symbolMapper, mapper) = BuildMappers();
        const string json = """
            {"pricebook":{"product_id":"BTC-USDT",
                "bids":[{"price":"41000","size":"0.5"}],
                "asks":[{"price":"41010","size":"0.7"}],
                "time":"2024-01-01T00:00:00Z"},"last":"41005"}
            """;
        var coinbaseHttp = BuildRealClient(_ => json);
        var service = new CoinbaseMarketDataService(coinbaseHttp, symbolMapper, mapper);

        var book = await service.GetOrderBookAsync(BtcUsdt, 50, TestContext.Current.CancellationToken);

        book.Symbol.Should().Be(BtcUsdt);
        book.Bids.Should().ContainSingle();
        book.Bids[0].Price.Should().Be(41000m);
        book.Asks[0].Quantity.Should().Be(0.7m);
    }

    [Fact]
    public async Task PostPropertyAsync_ExtractsResultsEnvelope_AndRefetchesOrder()
    {
        var (symbolMapper, mapper) = BuildMappers();
        const string cancelJson = """{"results":[{"order_id":"ord-real","success":true}]}""";
        const string orderJson = """
            {"order":{"order_id":"ord-real","product_id":"BTC-USDT","side":"SELL","status":"CANCELLED",
                "order_configuration":{"limit_limit_gtc":{"base_size":"1","limit_price":"30000","post_only":false}}}}
            """;
        var coinbaseHttp = BuildRealClient(req =>
            req.RequestUri!.AbsolutePath.Contains("batch_cancel", StringComparison.Ordinal) ? cancelJson : orderJson);
        var service = new CoinbaseTradingService(coinbaseHttp, symbolMapper, mapper);

        var order = await service.CancelOrderAsync(BtcUsdt, "ord-real", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("ord-real");
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public async Task GetPropertyAsync_MissingProperty_ReturnsDefault_OrderFallback()
    {
        var (symbolMapper, mapper) = BuildMappers();
        // Response shape lacks the "order" key; TryGetProperty yields default and the service falls back.
        var coinbaseHttp = BuildRealClient(_ => "{}");
        var service = new CoinbaseTradingService(coinbaseHttp, symbolMapper, mapper);

        var order = await service.GetOrderAsync(BtcUsdt, "missing-id", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("missing-id");
    }

    // Public market-data must hit Coinbase's unauthenticated /api/v3/brokerage/market/... endpoints.
    // The asserting handler fails offline (no network/creds) if a method regresses to an authed path.

    [Fact]
    public async Task GetExchangeInfo_RequestsPublicProductsPath()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = BuildAssertingClient("/api/v3/brokerage/market/products", """{"products":[]}""");
        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);

        var info = await service.GetExchangeInfoAsync(TestContext.Current.CancellationToken);
        info.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTickers_List_RequestsPublicProductsPath()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = BuildAssertingClient("/api/v3/brokerage/market/products", """{"products":[]}""");
        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);

        var tickers = await service.GetTickersAsync(ct: TestContext.Current.CancellationToken);
        tickers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTickers_SingleSymbol_RequestsPublicProductPath()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = BuildAssertingClient("/api/v3/brokerage/market/products/BTC-USDT", """{"product_id":"BTC-USDT","price":"42000"}""");
        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);

        var tickers = await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);
        tickers.Should().HaveCount(1);
        tickers[0].LastPrice.Should().Be(42000m);
    }

    [Fact]
    public async Task GetPrice_RequestsPublicProductPath()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = BuildAssertingClient("/api/v3/brokerage/market/products/BTC-USDT", """{"product_id":"BTC-USDT","price":"42000"}""");
        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);

        var price = await service.GetPriceAsync(BtcUsdt, TestContext.Current.CancellationToken);
        price.Should().Be(42000m);
    }

    [Fact]
    public async Task GetOrderBook_RequestsPublicProductBookPath()
    {
        var (symbolMapper, mapper) = BuildMappers();
        const string json = """{"pricebook":{"product_id":"BTC-USDT","bids":[],"asks":[],"time":"2024-01-01T00:00:00Z"}}""";
        var http = BuildAssertingClient("/api/v3/brokerage/market/product_book", json);
        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);

        var book = await service.GetOrderBookAsync(BtcUsdt, 10, TestContext.Current.CancellationToken);
        book.Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task GetCandlesticks_RequestsPublicCandlesPath()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = BuildAssertingClient("/api/v3/brokerage/market/products/BTC-USDT/candles", """{"candles":[]}""");
        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);

        var candles = await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneMinute, ct: TestContext.Current.CancellationToken);
        candles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentTrades_RequestsPublicMarketTradesPath()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = BuildAssertingClient("/api/v3/brokerage/market/products/BTC-USDT/ticker", """{"trades":[]}""");
        var service = new CoinbaseMarketDataService(http, symbolMapper, mapper);

        var trades = await service.GetRecentTradesAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        trades.Should().BeEmpty();
    }

    // Asserts the requested path is the expected public endpoint AND that the call is unsigned, so a
    // regression to an authed path or a signed market-data call fails offline before any network call.
    private static CoinbaseHttpClient BuildAssertingClient(string expectedAbsolutePath, string json)
    {
        var handler = new CapturingHandler(r =>
        {
            r.RequestUri!.AbsolutePath.Should().Be(expectedAbsolutePath);
            Resilience.CoinbaseSigningRequest.IsSigned(r).Should().BeFalse();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.coinbase.com") };
        return new CoinbaseHttpClient(httpClient);
    }

    private static CoinbaseHttpClient BuildRealClient(Func<HttpRequestMessage, string> bodyFactory)
    {
        var handler = new CapturingHandler(r =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(bodyFactory(r)) });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.coinbase.com") };
        return new CoinbaseHttpClient(httpClient);
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private sealed class JwtInjectingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Simulate what the signing handler does: add a Bearer token if the request is marked signed.
            if (Resilience.CoinbaseSigningRequest.IsSigned(request))
            {
                request.Headers.Remove("Authorization");
                request.Headers.Add("Authorization", "Bearer fake-jwt");
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
