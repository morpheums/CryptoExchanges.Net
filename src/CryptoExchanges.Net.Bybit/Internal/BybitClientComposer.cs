using System.Globalization;
using CryptoExchanges.Net.Bybit.Auth;
using CryptoExchanges.Net.Bybit.Mapping;
using CryptoExchanges.Net.Bybit.Resilience;
using CryptoExchanges.Net.Bybit.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bybit.Internal;

/// <summary>Single composition point for a Bybit client — shared by the container-free
/// <see cref="BybitExchangeClient.Create"/> path and the DI registration.</summary>
internal static class BybitClientComposer
{
    /// <summary>Builds an IMapper from the Bybit response profile bound to the given symbol mapper.</summary>
    public static IMapper CreateMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new BybitResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    /// <summary>Container-free composition: builds the resilient HttpClient and the fully-wired client.</summary>
    public static BybitExchangeClient Create(BybitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var offsetHolder = new long[] { 0L };
        var hc = BuildResilientHttpClient(options, offsetHolder);
        var http = new BybitHttpClient(hc);
        return ComposeOver(http, hc, ownsHttpClient: true, offsetHolder, new ExchangeTimeSync());
    }

    /// <summary>Wires symbol mapper + IMapper + the 3 services + the client over a given http client.</summary>
    public static BybitExchangeClient ComposeOver(
        IBybitHttpClient http, HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder,
        IExchangeTimeSync timeSync)
    {
        ISymbolMapper symbolMapper = new SymbolMapper(BybitSymbolFormat.Instance);
        return ComposeWith(http, symbolMapper, CreateMapper(symbolMapper), httpClient, ownsHttpClient, offsetHolder, timeSync);
    }

    /// <summary>Wires the 3 services + client using already-resolved symbol mapper + IMapper (DI path).</summary>
    public static BybitExchangeClient ComposeWith(
        IBybitHttpClient http, ISymbolMapper symbolMapper, IMapper mapper,
        HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder, IExchangeTimeSync timeSync)
    {
        var market = new BybitMarketDataService(http, symbolMapper, mapper);
        var trading = new BybitTradingService(http, symbolMapper, mapper);
        var account = new BybitAccountService(http, symbolMapper, mapper);
        return new BybitExchangeClient(http, market, trading, account, ownsHttpClient, httpClient, offsetHolder, timeSync);
    }

    /// <summary>DI composition: resolves the keyed symbol mapper + IMapper from the container and wires
    /// the services + client over the factory-owned http client (ownsHttpClient: false — IHttpClientFactory
    /// owns the underlying HttpClient lifetime).</summary>
    public static BybitExchangeClient ComposeForDi(IServiceProvider sp, IBybitHttpClient http, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(sp);
        var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Bybit);
        var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Bybit);
        var timeSync = sp.GetRequiredService<IExchangeTimeSync>();
        // INVARIANT: ownsHttpClient MUST stay false — IHttpClientFactory owns the HttpClient and its handler
        // chain; this client must NOT dispose it. Flipping this would double-dispose factory-owned state.
        return ComposeWith(http, symbolMapper, mapper, httpClient: null, ownsHttpClient: false, offsetHolder, timeSync);
    }

    /// <summary>Builds the resilient HttpClient (factory-less path) with a secret-gated signing finalizer
    /// over the shared offset holder. A secretless client gets a no-op PassThrough finalizer so public
    /// market-data works without credentials (mirrors the DI path's resolution-time gate).</summary>
    public static HttpClient BuildResilientHttpClient(BybitOptions options, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(offsetHolder);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        var resilienceOptions = new CryptoExchanges.Net.Core.Resilience.ResilienceOptions { UsageHeaderName = "X-Bapi-Limit-Status" };
        var translator = new BybitErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();

        // Options are final here, so the no-secret gate is decided now: a secretless client gets a
        // no-op PassThroughHandler instead of a BybitSigningHandler, mirroring the DI registration.
        var recvWindow = options.ReceiveWindow.ToString(CultureInfo.InvariantCulture);
        DelegatingHandler finalizer = string.IsNullOrEmpty(options.SecretKey)
            ? new CryptoExchanges.Net.Http.PassThroughHandler()
            : new BybitSigningHandler(
                options.ApiKey,
                new BybitSignatureService(options.SecretKey),
                recvWindow,
                () => Interlocked.Read(ref offsetHolder[0]));

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: finalizer);
        // No ExchangeUrl.NormalizeHostRoot here: Bybit signs only the query string (not RequestUri
        // path), so a BaseUrl path segment cannot corrupt the signature. A signing-sensitive exchange
        // that rebuilds its prehash from AbsolutePath/Query (OKX, Bitget) MUST use NormalizeHostRoot.
        hc.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        return hc;
    }
}
