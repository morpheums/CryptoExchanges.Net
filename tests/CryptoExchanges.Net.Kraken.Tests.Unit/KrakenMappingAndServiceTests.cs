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
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostAsync<Dtos.ResponseDto<Dtos.TradesFillsEnvelopeDto>>(
                "/0/private/TradesHistory", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.TradesFillsEnvelopeDto>
            {
                Result = new Dtos.TradesFillsEnvelopeDto
                {
                    Trades = new Dictionary<string, Dtos.FillDto>
                    {
                        ["T1"] = new Dtos.FillDto { Pair = "XBT/USDT", Price = "50000", Volume = "0.1", Side = "buy" },
                        ["T2"] = new Dtos.FillDto { Pair = "ETH/USD",  Price = "3000",  Volume = "1",   Side = "sell" }
                    }
                }
            });

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
        var http = Substitute.For<IKrakenHttpClient>();

        http.PostAsync<Dtos.ResponseDto<Dtos.OpenOrdersEnvelopeDto>>(
                "/0/private/OpenOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.OpenOrdersEnvelopeDto>
            {
                Result = new Dtos.OpenOrdersEnvelopeDto
                {
                    Open = new Dictionary<string, Dtos.OrderDto>
                    {
                        ["O1"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" }, Status = "open", Vol = "1" },
                        ["O2"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "ETH/USD",  Side = "sell", OrderType = "limit", Price = "3000"  }, Status = "open", Vol = "2" }
                    }
                }
            });

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOpenOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        orders.Should().HaveCount(1);
        orders[0].OrderId.Should().Be("O1");
    }

    [Fact]
    public async Task Trading_GetOrderHistory_AppliesTimeWindowParam()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IKrakenHttpClient>();
        Dictionary<string, string>? captured = null;

        http.PostAsync<Dtos.ResponseDto<Dtos.ClosedOrdersEnvelopeDto>>(
                "/0/private/ClosedOrders", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.ClosedOrdersEnvelopeDto>());

        var service = new KrakenTradingService(http, symbolMapper, mapper);
        var start = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var act = async () => await service.GetOrderHistoryAsync(BtcUsdt, startTime: start, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!.Should().ContainKey("start");
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

        http.PostAsync<Dtos.ResponseDto<Dtos.ClosedOrdersEnvelopeDto>>(
                "/0/private/ClosedOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.ClosedOrdersEnvelopeDto>
            {
                Result = new Dtos.ClosedOrdersEnvelopeDto
                {
                    Closed = new Dictionary<string, Dtos.OrderDto>
                    {
                        ["ORD-CLI-1"] = new Dtos.OrderDto
                        {
                            Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" },
                            Status = "canceled",
                            Vol = "0.1",
                            UserRef = 42
                        }
                    }
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

        http.PostAsync<Dtos.ResponseDto<Dtos.OpenOrdersEnvelopeDto>>(
                "/0/private/OpenOrders", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new Dtos.ResponseDto<Dtos.OpenOrdersEnvelopeDto>
            {
                Result = new Dtos.OpenOrdersEnvelopeDto
                {
                    Open = new Dictionary<string, Dtos.OrderDto>
                    {
                        ["ORD-A"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "buy", OrderType = "limit", Price = "50000" }, Status = "open", Vol = "0.5" },
                        ["ORD-B"] = new Dtos.OrderDto { Descr = new Dtos.OrderDescrDto { Pair = "XBT/USDT", Side = "sell", OrderType = "limit", Price = "55000" }, Status = "open", Vol = "0.3" }
                    }
                }
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

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
