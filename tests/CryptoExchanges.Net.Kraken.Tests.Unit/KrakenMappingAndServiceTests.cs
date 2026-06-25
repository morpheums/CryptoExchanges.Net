using Xunit;
using AwesomeAssertions;
using NSubstitute;
using DeltaMapper;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using CryptoExchanges.Net.Kraken;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Kraken.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Kraken.Tests.Unit;

/// <summary>
/// No-network unit tests for the Kraken DeltaMapper profile (DTO → domain model) and the
/// account/trading services exercised over a mocked <see cref="IKrakenHttpClient"/> (NSubstitute).
/// Includes signed-call header assertions and in-body-error translation via KrakenErrorTranslator.
/// </summary>
public class KrakenMappingAndServiceTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (ISymbolMapper symbolMapper, IMapper mapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(KrakenSymbolFormat.Instance);
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = KrakenClientComposer.CreateMapper(symbolMapper);
        return (symbolMapper, mapper);
    }

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        var act = () => BuildMappers();
        act.Should().NotThrow();
    }

    [Fact]
    public void OrderProfile_MapsAllScalars()
    {
        var (_, mapper) = BuildMappers();
        var dto = new Dtos.OrderDto
        {
            Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" },
            Status = "open",
            Vol = "0.5",
            VolExec = "0.1",
            Cost = "5000",
            OpenTime = 1700000000m,
            UserRef = 42
        };

        var order = mapper.Map<Dtos.OrderDto, Order>(dto);

        order.Price.Should().Be(50000m);
        order.OriginalQuantity.Should().Be(0.5m);
        order.ExecutedQuantity.Should().Be(0.1m);
        order.CumulativeQuoteQuantity.Should().Be(5000m);
        order.Side.Should().Be(OrderSide.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.Status.Should().Be(OrderStatus.New);
        order.TimeInForce.Should().Be(TimeInForce.Gtc);
        order.ClientOrderId.Should().Be("42");
        order.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public void BalanceProfile_MapsAssetAndAmount()
    {
        var (_, mapper) = BuildMappers();
        // Kraken returns USDT directly (no alias needed), so it maps to a recognised asset.
        var dto = new Dtos.BalanceDto { Asset = "USDT", Balance = "1.5" };

        var balance = mapper.Map<Dtos.BalanceDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Usdt);
        balance.Free.Should().Be(1.5m);
        balance.Locked.Should().Be(0m);
        balance.Total.Should().Be(1.5m);
    }

    [Fact]
    public void BalanceProfile_KrakenAlias_PreservesWireTicker()
    {
        var (_, mapper) = BuildMappers();
        // Asset is an open set: XXBT passes Asset.TryOf and is stored as-is.
        var dto = new Dtos.BalanceDto { Asset = "XXBT", Balance = "1.5" };

        var balance = mapper.Map<Dtos.BalanceDto, AssetBalance>(dto);

        balance.Asset.Ticker.Should().Be("XXBT");
        balance.Free.Should().Be(1.5m);
    }

    [Fact]
    public void FillProfile_MapsTradeFields()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var dto = new Dtos.FillDto
        {
            Pair = "XBT/USDT",
            Price = "48000",
            Volume = "0.25",
            Time = 1700000000m,
            Side = "buy",
            Maker = true,
            OrderTxId = "ORD-1"
        };

        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var trade = mapper.Map<Dtos.FillDto, Trade>(dto);

        trade.Price.Should().Be(48000m);
        trade.Quantity.Should().Be(0.25m);
        trade.OrderId.Should().Be("ORD-1");
        trade.IsBuyerMaker.Should().BeTrue();
    }

    [Fact]
    public void SymbolInfoProfile_MapsBaseQuote()
    {
        var (_, mapper) = BuildMappers();
        var dto = new Dtos.SymbolInfoDto { Wsname = "XBT/USDT", Base = "XXBT", Quote = "USDT" };

        var info = mapper.Map<Dtos.SymbolInfoDto, SymbolInfo>(dto);

        info.Symbol.Should().Be(BtcUsdt);
        info.AllowedOrderTypes.Should().Contain(OrderType.Limit).And.Contain(OrderType.Market);
    }

    [Fact]
    public async Task Account_GetBalances_TrimsZeroBalances()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();
        // Use tickers that Asset.TryOf resolves: USDT is recognised; ETH is recognised.
        http.PostAsync<Dtos.ResponseDto<Dictionary<string, string>>>(
                "/0/private/Balance", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, string>>
            {
                Result = new Dictionary<string, string> { ["USDT"] = "1000", ["ETH"] = "0" }
            });

        var service = new KrakenAccountService(http, symbolMapper, mapper);
        var balances = await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        balances.Should().HaveCount(1);
        balances[0].Asset.Should().Be(Asset.Usdt);
        balances[0].Total.Should().Be(1000m);
    }

    [Fact]
    public async Task Account_GetTradeHistory_FiltersToSymbol()
    {
        var (symbolMapper, mapper) = BuildMappers();
        // Full Kraken envelope: fills nested under result -> trades, extracted by the HTTP client.
        const string body = """
            {"error":[],"result":{"trades":{
                "T1":{"pair":"XBT/USDT","price":"50000","vol":"0.1","type":"buy"},
                "T2":{"pair":"ETH/USD","price":"3000","vol":"1","type":"sell"}
            }}}
            """;
        var handler = new FakeHttpHandler(_ => Ok(body));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com") };
        var http = new KrakenHttpClient(httpClient, symbolMapper);

        var service = new KrakenAccountService(http, symbolMapper, mapper);
        var trades = await service.GetTradeHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);

        trades.Should().HaveCount(1);
        trades[0].Price.Should().Be(50000m);
    }

    [Fact]
    public async Task Trading_PlaceOrder_SendsCorrectBodyAndRefetches()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        Dictionary<string, string>? placed = null;
        http.PostAsync<Dtos.ResponseDto<Dtos.OrderAckDto>>(
                "/0/private/AddOrder", Arg.Do<Dictionary<string, string>>(p => placed = p), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.OrderAckDto>
            {
                Result = new Dtos.OrderAckDto { TxId = ["ORD-123"] }
            });

        http.PostAsync<Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>>(
                "/0/private/QueryOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>
            {
                Result = new Dictionary<string, Dtos.OrderDto>
                {
                    ["ORD-123"] = new Dtos.OrderDto
                    {
                        Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" },
                        Status = "open",
                        Vol = "0.5"
                    }
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Limit, price: 50000m, quantity: 0.5m);
        var order = await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placed.Should().NotBeNull();
        placed!["pair"].Should().Be("XBT/USDT");
        placed["type"].Should().Be("buy");
        placed["ordertype"].Should().Be("limit");
        order.OrderId.Should().Be("ORD-123");
        order.Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task Trading_GetOpenOrders_FiltersToSymbol()
    {
        var (symbolMapper, mapper) = BuildMappers();
        // Full Kraken envelope: orders nested under result -> open, extracted by the HTTP client.
        const string body = """
            {"error":[],"result":{"open":{
                "O1":{"descr":{"pair":"XBT/USDT","type":"buy","ordertype":"limit","price":"50000"},"status":"open","vol":"1"},
                "O2":{"descr":{"pair":"ETH/USD","type":"sell","ordertype":"limit","price":"3000"},"status":"open","vol":"2"}
            }}}
            """;
        var handler = new FakeHttpHandler(_ => Ok(body));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com") };
        var http = new KrakenHttpClient(httpClient, symbolMapper);

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOpenOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        orders.Should().HaveCount(1);
        orders[0].OrderId.Should().Be("O1");
    }

    [Fact]
    public async Task Trading_GetOrderHistory_AppliesTimeWindowParam()
    {
        var (symbolMapper, mapper) = BuildMappers();
        string? capturedBody = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Ok("""{"error":[],"result":{"closed":{}}}""");
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com") };
        var http = new KrakenHttpClient(httpClient, symbolMapper);

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var start = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var act = async () => await service.GetOrderHistoryAsync(BtcUsdt, startTime: start, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        capturedBody.Should().NotBeNull();
        capturedBody!.Should().Contain("start");
    }

    [Fact]
    public async Task SignedCall_SetsApiKeyAndApiSignHeaders()
    {
        const string apiKey = "test-key";
        // base64("test-secret-for-unit-test-32byte") — must be valid base64 and decode to ≥1 byte.
        const string apiSecret = "dGVzdC1zZWNyZXQtZm9yLXVuaXQtdGVzdC0zMmJ5dGU=";

        HttpRequestMessage? captured = null;
        var fakeHandler = new FakeHttpHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"error":[],"result":{"XXBT":"1.0"}}""",
                    Encoding.UTF8, "application/json")
            };
        });

        var sigSvc = new CryptoExchanges.Net.Kraken.Auth.KrakenSignatureService(apiSecret);
        var signingHandler = new CryptoExchanges.Net.Kraken.Resilience.KrakenSigningHandler(apiKey, sigSvc) { InnerHandler = fakeHandler };
        using var httpClient = new HttpClient(signingHandler) { BaseAddress = new Uri("https://api.kraken.com") };
        var symbolMapper = new SymbolMapper(KrakenSymbolFormat.Instance);
        var krakenHttp = new KrakenHttpClient(httpClient, symbolMapper);

        _ = await krakenHttp.PostAsync<Dtos.ResponseDto<Dictionary<string, string>>>(
            "/0/private/Balance", signed: true);

        captured.Should().NotBeNull();
        captured!.Headers.Should().ContainSingle(h => h.Key == "API-Key");
        captured.Headers.GetValues("API-Key").Should().ContainSingle().Which.Should().Be(apiKey);
        captured.Headers.Should().ContainSingle(h => h.Key == "API-Sign");
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
    }

    [Fact]
    public async Task InBodyError_GenericPrefix_ThrowsExchangeApiException()
    {
        const string errorBody = """{"error":["EGeneral:Invalid arguments"],"result":null}""";
        var translator = new CryptoExchanges.Net.Kraken.Resilience.KrakenErrorTranslator();
        var ex = translator.Translate(new HttpResponseMessage(HttpStatusCode.OK), errorBody);
        ex.Should().BeAssignableTo<Core.Exceptions.ExchangeException>();
        ex.Message.Should().Contain("EGeneral:Invalid arguments");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task InBodyError_AuthPrefix_ThrowsAuthenticationException()
    {
        const string errorBody = """{"error":["EAuth:Invalid key"],"result":null}""";
        var translator = new CryptoExchanges.Net.Kraken.Resilience.KrakenErrorTranslator();
        var ex = translator.Translate(new HttpResponseMessage(HttpStatusCode.OK), errorBody);
        ex.Should().BeAssignableTo<Core.Exceptions.AuthenticationException>();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task InBodyError_EOrderPrefix_ThrowsInvalidOrderException()
    {
        const string errorBody = """{"error":["EOrder:Insufficient funds"],"result":null}""";
        var translator = new CryptoExchanges.Net.Kraken.Resilience.KrakenErrorTranslator();
        var ex = translator.Translate(new HttpResponseMessage(HttpStatusCode.OK), errorBody);
        ex.Should().BeAssignableTo<Core.Exceptions.InvalidOrderException>();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Di_AddKrakenExchange_ResolvesKeyedClient()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange(o => { o.ApiKey = "k"; o.ApiSecret = "dGVzdC1zZWNyZXQtZm9yLXVuaXQtdGVzdC0zMmJ5dGU="; });
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kraken);
        client.ExchangeId.Should().Be(ExchangeId.Kraken);
    }

    [Fact]
    public async Task Di_AddKrakenExchange_Secretless_StillResolvesWorkingClient()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kraken).ExchangeId.Should().Be(ExchangeId.Kraken);
    }

    [Fact]
    public async Task Di_AddKrakenExchange_IsScopeClean()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange();
        await using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kraken).Should().NotBeNull();
    }

    [Fact]
    public void Di_AddKrakenExchange_MapperIsKeyedSingleton()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange();
        using var sp = services.BuildServiceProvider();

        var m1 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Kraken);
        var m2 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Kraken);
        m1.Should().BeSameAs(m2);
    }

    [Fact]
    public void Di_AddKrakenExchange_InvalidOptions_FailFast()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange(o => o.TimeoutSeconds = 0);
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kraken);
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public async Task Account_GetBalance_ReturnsMatchingAsset()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();
        http.PostAsync<Dtos.ResponseDto<Dictionary<string, string>>>(
                "/0/private/Balance", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, string>>
            {
                Result = new Dictionary<string, string> { ["USDT"] = "500", ["XXBT"] = "0.1" }
            });

        var service = new KrakenAccountService(http, symbolMapper, mapper);
        var balance = await service.GetBalanceAsync(Asset.Usdt, TestContext.Current.CancellationToken);

        balance.Asset.Should().Be(Asset.Usdt);
        balance.Total.Should().Be(500m);
    }

    [Fact]
    public async Task Trading_CancelOrder_CallsCancelAndRefetches()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostAsync<Dtos.ResponseDto<System.Text.Json.JsonElement>>(
                "/0/private/CancelOrder", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<System.Text.Json.JsonElement>());

        http.PostAsync<Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>>(
                "/0/private/QueryOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>
            {
                Result = new Dictionary<string, Dtos.OrderDto>
                {
                    ["ORD-999"] = new Dtos.OrderDto
                    {
                        Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "sell", OrderType = "limit", Price = "60000" },
                        Status = "canceled",
                        Vol = "0.2"
                    }
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderAsync(BtcUsdt, "ORD-999", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("ORD-999");
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public async Task Trading_CancelOrderByClientId_FetchesFromClosedOrders()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostAsync<Dtos.ResponseDto<System.Text.Json.JsonElement>>(
                "/0/private/CancelOrder", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<System.Text.Json.JsonElement>());

        http.PostResultPropertyAsync<Dictionary<string, Dtos.OrderDto>>(
                "/0/private/ClosedOrders", "closed", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Dtos.OrderDto>
            {
                ["ORD-CLI-1"] = new Dtos.OrderDto
                {
                    Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" },
                    Status = "canceled",
                    Vol = "0.1",
                    UserRef = 42
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        // clientOrderId must be parseable as int (Kraken userref).
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "42", TestContext.Current.CancellationToken);

        order.ClientOrderId.Should().Be("42");
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public async Task Trading_CancelAllOrders_CancelsEachOpenOrder()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostResultPropertyAsync<Dictionary<string, Dtos.OrderDto>>(
                "/0/private/OpenOrders", "open", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Dtos.OrderDto>
            {
                ["ORD-A"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" }, Status = "open", Vol = "0.5" },
                ["ORD-B"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "sell", OrderType = "limit", Price = "55000" }, Status = "open", Vol = "0.3" }
            });

        http.PostAsync<Dtos.ResponseDto<System.Text.Json.JsonElement>>(
                "/0/private/CancelOrder", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<System.Text.Json.JsonElement>());

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var canceled = await service.CancelAllOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        canceled.Should().HaveCount(2);
        canceled.Should().AllSatisfy(o => o.Status.Should().Be(OrderStatus.Canceled));
    }

    [Fact]
    public async Task Trading_GetOrder_QueriesAndMapsOrder()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostAsync<Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>>(
                "/0/private/QueryOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>
            {
                Result = new Dictionary<string, Dtos.OrderDto>
                {
                    ["ORD-XYZ"] = new Dtos.OrderDto
                    {
                        Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "49000" },
                        Status = "open",
                        Vol = "1"
                    }
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var order = await service.GetOrderAsync(BtcUsdt, "ORD-XYZ", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("ORD-XYZ");
        order.Price.Should().Be(49000m);
        order.Status.Should().Be(OrderStatus.New);
    }

    [Fact]
    public async Task HttpClient_InBodyError_On200_ThrowsTranslatedException()
    {
        // Kraken signals failures as HTTP 200 + error[]; the client must translate, not deserialize.
        var fakeHandler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"error":["EAuth:Invalid key"],"result":null}""",
                Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.kraken.com") };
        var krakenHttp = new KrakenHttpClient(httpClient, new SymbolMapper(KrakenSymbolFormat.Instance));

        var act = async () => await krakenHttp.GetAsync<Dtos.ResponseDto<Dictionary<string, string>>>(
            "/0/public/Ticker", ct: TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<Core.Exceptions.AuthenticationException>())
            .Which.Message.Should().Contain("EAuth:");
    }

    [Fact]
    public async Task PostResultProperty_InBodyError_On200_ThrowsTranslatedException()
    {
        // The named-property extraction path must still surface in-body error[] before navigating result.
        var fakeHandler = new FakeHttpHandler(_ => Ok("""{"error":["EAuth:Invalid key"],"result":null}"""));
        using var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.kraken.com") };
        var krakenHttp = new KrakenHttpClient(httpClient, new SymbolMapper(KrakenSymbolFormat.Instance));

        var act = async () => await krakenHttp.PostResultPropertyAsync<Dictionary<string, Dtos.OrderDto>>(
            "/0/private/OpenOrders", "open", ct: TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<Core.Exceptions.AuthenticationException>())
            .Which.Message.Should().Contain("EAuth:");
    }

    [Fact]
    public async Task PostResultProperty_AbsentProperty_ReturnsDefault()
    {
        // No-NRE parity: an absent result/property yields default (null), which services coalesce to empty.
        var fakeHandler = new FakeHttpHandler(_ => Ok("""{"error":[],"result":{}}"""));
        using var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.kraken.com") };
        var krakenHttp = new KrakenHttpClient(httpClient, new SymbolMapper(KrakenSymbolFormat.Instance));

        var result = await krakenHttp.PostResultPropertyAsync<Dictionary<string, Dtos.OrderDto>>(
            "/0/private/OpenOrders", "open", ct: TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task HttpClient_EmptyErrorArray_On200_DeserializesResult()
    {
        var fakeHandler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"error":[],"result":{"USDT":"1.0"}}""",
                Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.kraken.com") };
        var krakenHttp = new KrakenHttpClient(httpClient, new SymbolMapper(KrakenSymbolFormat.Instance));

        var result = await krakenHttp.GetAsync<Dtos.ResponseDto<Dictionary<string, string>>>(
            "/0/public/Ticker", ct: TestContext.Current.CancellationToken);

        result.Result.Should().ContainKey("USDT");
    }

    [Fact]
    public async Task Trading_PlaceOrder_QuoteQuantity_ThrowsNotSupported()
    {
        // Kraken AddOrder 'volume' is base-asset only; a quote-denominated quantity must be rejected,
        // never stuffed into 'volume' (which would mis-size the order).
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();
        var service = new KrakenTradingService(http, symbolMapper, mapper);

        var request = PlaceOrderRequest.Create(
            BtcUsdt, OrderSide.Buy, OrderType.Market, quoteOrderQuantity: 1000m);
        var act = async () => await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
        await http.DidNotReceive().PostAsync<Dtos.ResponseDto<Dtos.OrderAckDto>>(
            "/0/private/AddOrder", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Trading_PlaceOrder_BaseQuantity_GoesIntoVolume()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        Dictionary<string, string>? placed = null;
        http.PostAsync<Dtos.ResponseDto<Dtos.OrderAckDto>>(
                "/0/private/AddOrder", Arg.Do<Dictionary<string, string>>(p => placed = p), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.OrderAckDto> { Result = new Dtos.OrderAckDto { TxId = ["ORD-1"] } });
        http.PostAsync<Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>>(
                "/0/private/QueryOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>
            {
                Result = new Dictionary<string, Dtos.OrderDto>
                {
                    ["ORD-1"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "market" }, Status = "open", Vol = "0.25" }
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Buy, OrderType.Market, quantity: 0.25m);
        await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placed.Should().NotBeNull();
        placed!["volume"].Should().Be("0.25");
    }

    [Fact]
    public async Task Trading_CancelOrderByClientId_NoClosedMatch_CarriesClientIdInClientOrderIdNotOrderId()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostAsync<Dtos.ResponseDto<System.Text.Json.JsonElement>>(
                "/0/private/CancelOrder", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<System.Text.Json.JsonElement>());

        // ClosedOrders returns no match for the userref, forcing the fallback Order.
        http.PostResultPropertyAsync<Dictionary<string, Dtos.OrderDto>>(
                "/0/private/ClosedOrders", "closed", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Dtos.OrderDto>());

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "42", TestContext.Current.CancellationToken);

        order.ClientOrderId.Should().Be("42");
        order.OrderId.Should().BeEmpty();
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Theory]
    [InlineData(OrderType.StopLoss, "stop-loss")]
    [InlineData(OrderType.TakeProfit, "take-profit")]
    public async Task Trading_PlaceStopOrder_MapsOrderTypeAndTriggerPrice(OrderType type, string expectedOrderType)
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        Dictionary<string, string>? placed = null;
        http.PostAsync<Dtos.ResponseDto<Dtos.OrderAckDto>>(
                "/0/private/AddOrder", Arg.Do<Dictionary<string, string>>(p => placed = p), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.OrderAckDto> { Result = new Dtos.OrderAckDto { TxId = ["ORD-S"] } });
        http.PostAsync<Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>>(
                "/0/private/QueryOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>
            {
                Result = new Dictionary<string, Dtos.OrderDto>
                {
                    ["ORD-S"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "sell", OrderType = expectedOrderType }, Status = "open", Vol = "0.5" }
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Sell, type, quantity: 0.5m, stopPrice: 45000m);
        await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placed.Should().NotBeNull();
        placed!["ordertype"].Should().Be(expectedOrderType);
        // Trigger price is carried in Kraken's 'price' param; no 'price2' for non-limit stop orders.
        placed["price"].Should().Be("45000");
        placed.ContainsKey("price2").Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderType.StopLossLimit, "stop-loss-limit")]
    [InlineData(OrderType.TakeProfitLimit, "take-profit-limit")]
    public async Task Trading_PlaceStopLimitOrder_SendsTriggerInPriceAndLimitInPrice2(OrderType type, string expectedOrderType)
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        Dictionary<string, string>? placed = null;
        http.PostAsync<Dtos.ResponseDto<Dtos.OrderAckDto>>(
                "/0/private/AddOrder", Arg.Do<Dictionary<string, string>>(p => placed = p), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.OrderAckDto> { Result = new Dtos.OrderAckDto { TxId = ["ORD-SL"] } });
        http.PostAsync<Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>>(
                "/0/private/QueryOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dictionary<string, Dtos.OrderDto>>
            {
                Result = new Dictionary<string, Dtos.OrderDto>
                {
                    ["ORD-SL"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "sell", OrderType = expectedOrderType, Price = "44000" }, Status = "open", Vol = "0.5" }
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var request = PlaceOrderRequest.Create(BtcUsdt, OrderSide.Sell, type, quantity: 0.5m, price: 44000m, stopPrice: 45000m);
        await service.PlaceOrderAsync(request, TestContext.Current.CancellationToken);

        placed.Should().NotBeNull();
        placed!["ordertype"].Should().Be(expectedOrderType);
        placed["price"].Should().Be("45000");
        placed["price2"].Should().Be("44000");
    }

    [Fact]
    public async Task Trading_GetOrderHistory_OrdersByCloseTimeDescending_AppliesLimit()
    {
        // ClosedOrders enumeration order is non-deterministic; the service must sort most-recent-first
        // before Take(limit) so the latest N are returned in order, not an arbitrary subset.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostResultPropertyAsync<Dictionary<string, Dtos.OrderDto>>(
                "/0/private/ClosedOrders", "closed", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Dtos.OrderDto>
            {
                ["OLD"] = ClosedOrder(closeTime: 100m),
                ["NEW"] = ClosedOrder(closeTime: 300m),
                ["MID"] = ClosedOrder(closeTime: 200m)
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOrderHistoryAsync(BtcUsdt, limit: 2, ct: TestContext.Current.CancellationToken);

        orders.Select(o => o.OrderId).Should().Equal("NEW", "MID");
    }

    [Fact]
    public async Task Trading_CancelOrderByClientId_MultipleClosed_ReturnsMostRecentTxId()
    {
        // When several closed orders share the userref, selection must be deterministic (most recent by
        // close time), not the arbitrary first dictionary entry.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostAsync<Dtos.ResponseDto<System.Text.Json.JsonElement>>(
                "/0/private/CancelOrder", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<System.Text.Json.JsonElement>());

        http.PostResultPropertyAsync<Dictionary<string, Dtos.OrderDto>>(
                "/0/private/ClosedOrders", "closed", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Dtos.OrderDto>
            {
                ["ORD-OLD"] = ClosedOrder(closeTime: 100m, userRef: 42),
                ["ORD-NEW"] = ClosedOrder(closeTime: 300m, userRef: 42),
                ["ORD-MID"] = ClosedOrder(closeTime: 200m, userRef: 42)
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "42", TestContext.Current.CancellationToken);

        order.OrderId.Should().Be("ORD-NEW");
        order.ClientOrderId.Should().Be("42");
    }

    [Fact]
    public async Task Account_GetTradeHistory_OrdersByTimeDescending_AppliesLimit()
    {
        // TradesHistory enumeration order is non-deterministic; the service must sort most-recent-first
        // before Take(limit) so the latest N fills are returned in order, not an arbitrary subset.
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostResultPropertyAsync<Dictionary<string, Dtos.FillDto>>(
                "/0/private/TradesHistory", "trades", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Dtos.FillDto>
            {
                ["T-OLD"] = new Dtos.FillDto { Pair = "XBT/USDT", Price = "1", Volume = "0.1", Time = 100m, Side = "buy" },
                ["T-NEW"] = new Dtos.FillDto { Pair = "XBT/USDT", Price = "3", Volume = "0.1", Time = 300m, Side = "buy" },
                ["T-MID"] = new Dtos.FillDto { Pair = "XBT/USDT", Price = "2", Volume = "0.1", Time = 200m, Side = "buy" }
            });

        var service = new KrakenAccountService(http, symbolMapper, mapper);
        var trades = await service.GetTradeHistoryAsync(BtcUsdt, limit: 2, ct: TestContext.Current.CancellationToken);

        trades.Select(t => t.Price).Should().Equal(3m, 2m);
    }

    private static Dtos.OrderDto ClosedOrder(decimal closeTime, int? userRef = null) => new()
    {
        Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" },
        Status = "closed",
        Vol = "0.1",
        CloseTime = closeTime,
        UserRef = userRef
    };

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
