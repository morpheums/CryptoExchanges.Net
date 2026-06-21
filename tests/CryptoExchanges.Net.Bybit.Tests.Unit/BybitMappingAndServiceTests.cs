using Xunit;
using AwesomeAssertions;
using NSubstitute;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using CryptoExchanges.Net.Bybit;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Bybit.Mapping;
using CryptoExchanges.Net.Bybit.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Bybit.Tests.Unit;

/// <summary>
/// No-network unit tests for the Bybit DeltaMapper profile (DTO -> domain model) and the three
/// services exercised over a mocked <see cref="IBybitHttpClient"/> (NSubstitute). Includes the two
/// round-1 regression tests: history default-limit clamping and cancel-by-clientId orderId fallback.
/// </summary>
public class BybitMappingAndServiceTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (ISymbolMapper symbolMapper, IMapper mapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(BybitSymbolFormat.Instance);
        // Warm so FromWire("BTCUSDT") resolves to BTC/USDT exactly.
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = BybitClientComposer.CreateMapper(symbolMapper);
        return (symbolMapper, mapper);
    }

    // ── Profile mapping (representative V5 payloads) ──

    [Fact]
    public void OrderProfile_MapsAllScalarsAndResolvesSymbol()
    {
        var (_, mapper) = BuildMappers();
        var dto = new OrderDto
        {
            Symbol = "BTCUSDT",
            OrderId = "111",
            OrderLinkId = "cli-1",
            Price = "100.5",
            Qty = "2",
            CumExecQty = "1",
            CumExecValue = "150.75",
            Side = "Buy",
            OrderType = "Limit",
            OrderStatus = "PartiallyFilled",
            TimeInForce = "GTC",
            TriggerPrice = "0",
            CreatedTime = "1700000000000",
            UpdatedTime = "1700000001000"
        };

        var order = mapper.Map<OrderDto, Order>(dto);

        order.Symbol.Should().Be(BtcUsdt);
        order.OrderId.Should().Be("111");
        order.ClientOrderId.Should().Be("cli-1");
        order.Price.Should().Be(100.5m);
        order.OriginalQuantity.Should().Be(2m);
        order.ExecutedQuantity.Should().Be(1m);
        order.CumulativeQuoteQuantity.Should().Be(150.75m);
        order.Side.Should().Be(OrderSide.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.Status.Should().Be(OrderStatus.PartiallyFilled);
        order.TimeInForce.Should().Be(TimeInForce.Gtc);
        order.StopPrice.Should().BeNull();
        order.CreatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        order.UpdatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000001000));
    }

    [Fact]
    public void TickerProfile_ScalesPercentAndComputesChange()
    {
        var (_, mapper) = BuildMappers();
        var dto = new TickerDto
        {
            Symbol = "BTCUSDT",
            LastPrice = "42000",
            PrevPrice24h = "41000",
            HighPrice24h = "43000",
            LowPrice24h = "40000",
            Volume24h = "123.45",
            Turnover24h = "5000000",
            Price24hPcnt = "0.025"
        };

        var ticker = mapper.Map<TickerDto, Ticker>(dto);

        ticker.Symbol.Should().Be(BtcUsdt);
        ticker.LastPrice.Should().Be(42000m);
        ticker.OpenPrice.Should().Be(41000m);
        ticker.PriceChange.Should().Be(1000m);
        // price24hPcnt is a fraction (0.025); the profile scales it to a percent.
        ticker.PriceChangePercent.Should().Be(2.5m);
    }

    [Fact]
    public void InstrumentProfile_MapsBaseQuoteToSymbol()
    {
        var (_, mapper) = BuildMappers();
        var dto = new SymbolInfoDto { Symbol = "BTCUSDT", BaseCoin = "BTC", QuoteCoin = "USDT" };

        var info = mapper.Map<SymbolInfoDto, SymbolInfo>(dto);

        info.Symbol.Should().Be(BtcUsdt);
        info.AllowedOrderTypes.Should().Contain(OrderType.Limit).And.Contain(OrderType.Market);
    }

    [Fact]
    public void BalanceProfile_FreeIsWalletMinusLocked()
    {
        var (_, mapper) = BuildMappers();
        var dto = new BalanceDto { Coin = "BTC", WalletBalance = "1.75", Locked = "0.25" };

        var balance = mapper.Map<BalanceDto, AssetBalance>(dto);

        balance.Asset.Should().Be(Asset.Btc);
        balance.Free.Should().Be(1.5m);
        balance.Locked.Should().Be(0.25m);
        balance.Total.Should().Be(1.75m);
    }

    // ── MarketDataService over mocked IBybitHttpClient ──

    [Fact]
    public async Task MarketData_GetTickers_MapsRepresentativePayload()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBybitHttpClient>();
        http.GetAsync<ResponseDto<ListDto<TickerDto>>>(
                "/v5/market/tickers", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<TickerDto>>
            {
                Result = new ListDto<TickerDto> { List = [new TickerDto { Symbol = "BTCUSDT", LastPrice = "42000", PrevPrice24h = "41000" }] }
            });

        var service = new BybitMarketDataService(http, symbolMapper, mapper);
        var tickers = await service.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        tickers.Should().HaveCount(1);
        tickers[0].Symbol.Should().Be(BtcUsdt);
        tickers[0].LastPrice.Should().Be(42000m);
    }

    // ── AccountService over mocked IBybitHttpClient ──

    [Fact]
    public async Task Account_GetBalances_TrimsZeroBalances()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBybitHttpClient>();
        http.GetAsync<ResponseDto<ListDto<AccountDto>>>(
                "/v5/account/wallet-balance", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<AccountDto>>
            {
                Result = new ListDto<AccountDto>
                {
                    List =
                    [
                        new AccountDto
                        {
                            Coin =
                            [
                                new BalanceDto { Coin = "BTC", WalletBalance = "1.5", Locked = "0" },
                                new BalanceDto { Coin = "ZZZ", WalletBalance = "0", Locked = "0" }
                            ]
                        }
                    ]
                }
            });

        var service = new BybitAccountService(http, symbolMapper, mapper);
        var balances = await service.GetBalancesAsync(TestContext.Current.CancellationToken);

        balances.Should().HaveCount(1);
        balances[0].Asset.Should().Be(Asset.Btc);
        balances[0].Total.Should().Be(1.5m);
    }

    // ── TradingService over mocked IBybitHttpClient ──

    [Fact]
    public async Task Trading_GetOpenOrders_MapsOrders()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBybitHttpClient>();
        http.GetAsync<ResponseDto<ListDto<OrderDto>>>(
                "/v5/order/realtime", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<OrderDto>>
            {
                Result = new ListDto<OrderDto>
                {
                    List = [new OrderDto { Symbol = "BTCUSDT", OrderId = "9", Qty = "1", Price = "5" }]
                }
            });

        var service = new BybitTradingService(http, symbolMapper, mapper);
        var orders = await service.GetOpenOrdersAsync(BtcUsdt, TestContext.Current.CancellationToken);

        orders.Should().HaveCount(1);
        orders[0].OrderId.Should().Be("9");
        orders[0].Symbol.Should().Be(BtcUsdt);
    }

    // ── REGRESSION 1: default limit (500) clamps to 50 (not a validation throw) ──

    [Fact]
    public async Task Trading_GetOrderHistory_DefaultLimit_ClampsToFifty()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBybitHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<ListDto<OrderDto>>>(
                "/v5/order/history", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<OrderDto>> { Result = new ListDto<OrderDto>() });

        var service = new BybitTradingService(http, symbolMapper, mapper);

        // Default IExchangeClient limit is 500; the service must clamp to 50, not throw.
        var act = async () => await service.GetOrderHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("50");
    }

    [Fact]
    public async Task Account_GetTradeHistory_DefaultLimit_ClampsToFifty()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBybitHttpClient>();
        Dictionary<string, string>? captured = null;
        http.GetAsync<ResponseDto<ListDto<FillDto>>>(
                "/v5/execution/list", Arg.Do<Dictionary<string, string>>(p => captured = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<FillDto>> { Result = new ListDto<FillDto>() });

        var service = new BybitAccountService(http, symbolMapper, mapper);

        var act = async () => await service.GetTradeHistoryAsync(BtcUsdt, ct: TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!["limit"].Should().Be("50");
    }

    // ── REGRESSION 2: cancel-by-clientId with no orderId in ACK re-fetches by orderLinkId ──

    [Fact]
    public async Task Trading_CancelByClientId_AckOmitsOrderId_RefetchesByLinkIdWithNonEmptyId()
    {
        var (symbolMapper, mapper) = BuildMappers();
        var http = Substitute.For<IBybitHttpClient>();

        // The cancel ACK omits orderId (only orderLinkId is echoed).
        http.PostAsync<ResponseDto<OrderAckDto>>(
                "/v5/order/cancel", Arg.Any<Dictionary<string, string>>(), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<OrderAckDto> { Result = new OrderAckDto { OrderId = string.Empty, OrderLinkId = "cli-77" } });

        // The re-fetch must query by orderLinkId (orderId is empty) and resolve the real order.
        Dictionary<string, string>? refetchParams = null;
        http.GetAsync<ResponseDto<ListDto<OrderDto>>>(
                "/v5/order/realtime", Arg.Do<Dictionary<string, string>>(p => refetchParams = p), true, Arg.Any<CancellationToken>())
            .Returns(new ResponseDto<ListDto<OrderDto>>
            {
                Result = new ListDto<OrderDto>
                {
                    List = [new OrderDto { Symbol = "BTCUSDT", OrderId = "real-99", OrderLinkId = "cli-77", OrderStatus = "Cancelled" }]
                }
            });

        var service = new BybitTradingService(http, symbolMapper, mapper);
        var order = await service.CancelOrderByClientIdAsync(BtcUsdt, "cli-77", TestContext.Current.CancellationToken);

        // The returned order must carry a non-empty id resolved via the orderLinkId fallback.
        order.OrderId.Should().Be("real-99");
        order.OrderId.Should().NotBeNullOrEmpty();
        refetchParams.Should().NotBeNull();
        refetchParams!.Should().ContainKey("orderLinkId").WhoseValue.Should().Be("cli-77");
        refetchParams.Should().NotContainKey("orderId");
    }

    // ── DI resolution ──

    [Fact]
    public async Task Di_AddBybitExchange_ResolvesKeyedClient()
    {
        var services = new ServiceCollection();
        services.AddBybitExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        // await using: the resolved IExchangeClient is IAsyncDisposable-only, so the container must
        // be disposed asynchronously (a synchronous Dispose throws on that async-only disposable).
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit);
        client.ExchangeId.Should().Be(ExchangeId.Bybit);
    }

    [Fact]
    public async Task Di_AddBybitExchange_Secretless_StillResolvesWorkingClient()
    {
        // A secretless registration must resolve (public market data needs no credentials); the
        // finalizer is a PassThroughHandler rather than a signing handler in this path.
        var services = new ServiceCollection();
        services.AddBybitExchange();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit);
        client.ExchangeId.Should().Be(ExchangeId.Bybit);
    }

    [Fact]
    public void Di_AddBybitExchange_MapperIsKeyedSingleton()
    {
        var services = new ServiceCollection();
        services.AddBybitExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        using var sp = services.BuildServiceProvider();

        var m1 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Bybit);
        var m2 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Bybit);
        m1.Should().BeSameAs(m2);
    }

    [Fact]
    public void Di_AddBybitExchange_InvalidOptions_FailFast()
    {
        var services = new ServiceCollection();
        services.AddBybitExchange(o => o.TimeoutSeconds = 0);
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit);
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public async Task Di_AddBybitExchange_IsScopeClean()
    {
        var services = new ServiceCollection();
        services.AddBybitExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        await using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit).Should().NotBeNull();
    }
}
