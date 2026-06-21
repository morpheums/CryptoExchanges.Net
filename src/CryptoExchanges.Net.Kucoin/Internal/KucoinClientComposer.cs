using CryptoExchanges.Net.Kucoin.Auth;
using CryptoExchanges.Net.Kucoin.Mapping;
using CryptoExchanges.Net.Kucoin.Resilience;
using CryptoExchanges.Net.Kucoin.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kucoin.Internal;

/// <summary>Single composition point for a KuCoin client — shared by the container-free
/// <see cref="KucoinExchangeClient.Create"/> path and the DI registration.</summary>
internal static class KucoinClientComposer
{
    /// <summary>Builds an IMapper from the KuCoin response profile bound to the given symbol mapper.</summary>
    public static IMapper CreateMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new KucoinResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    /// <summary>Container-free composition: builds the resilient HttpClient and the fully-wired client.</summary>
    public static KucoinExchangeClient Create(KucoinOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var offsetHolder = new long[] { 0L };
        var hc = BuildResilientHttpClient(options, offsetHolder);
        var http = new KucoinHttpClient(hc);
        return ComposeOver(http, hc, ownsHttpClient: true, offsetHolder, new ExchangeTimeSync());
    }

    /// <summary>Wires symbol mapper + IMapper + the 3 services + the client over a given http client.</summary>
    public static KucoinExchangeClient ComposeOver(
        IKucoinHttpClient http, HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder,
        IExchangeTimeSync timeSync)
    {
        ISymbolMapper symbolMapper = new KucoinSymbolMapper();
        return ComposeWith(http, symbolMapper, CreateMapper(symbolMapper), httpClient, ownsHttpClient, offsetHolder, timeSync);
    }

    /// <summary>Wires the 3 services + client using already-resolved symbol mapper + IMapper (DI path).</summary>
    public static KucoinExchangeClient ComposeWith(
        IKucoinHttpClient http, ISymbolMapper symbolMapper, IMapper mapper,
        HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder, IExchangeTimeSync timeSync)
    {
        var market = new KucoinMarketDataService(http, symbolMapper, mapper);
        var trading = new KucoinTradingService(http, symbolMapper, mapper);
        var account = new KucoinAccountService(http, symbolMapper, mapper);
        return new KucoinExchangeClient(http, market, trading, account, ownsHttpClient, httpClient, offsetHolder, timeSync);
    }

    /// <summary>DI composition: resolves the keyed symbol mapper + IMapper from the container and wires
    /// the services + client over the factory-owned http client (ownsHttpClient: false — IHttpClientFactory
    /// owns the underlying HttpClient lifetime).</summary>
    public static KucoinExchangeClient ComposeForDi(IServiceProvider sp, IKucoinHttpClient http, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(sp);
        var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Kucoin);
        var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Kucoin);
        var timeSync = sp.GetRequiredService<IExchangeTimeSync>();
        // INVARIANT: ownsHttpClient MUST stay false — IHttpClientFactory owns the HttpClient and its
        // handler chain; this client must NOT dispose it.
        return ComposeWith(http, symbolMapper, mapper, httpClient: null, ownsHttpClient: false, offsetHolder, timeSync);
    }

    /// <summary>Builds the resilient HttpClient (factory-less path). A client missing the secret OR the
    /// passphrase gets a no-op PassThrough finalizer so public market-data works without full credentials.</summary>
    public static HttpClient BuildResilientHttpClient(KucoinOptions options, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(offsetHolder);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        var resilienceOptions = new CryptoExchanges.Net.Core.Resilience.ResilienceOptions();
        var translator = new KucoinErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();

        DelegatingHandler finalizer =
            (string.IsNullOrEmpty(options.SecretKey) || string.IsNullOrEmpty(options.Passphrase))
                ? new CryptoExchanges.Net.Http.PassThroughHandler()
                : new KucoinSigningHandler(
                    options.ApiKey,
                    options.Passphrase,
                    new KucoinSignatureService(options.SecretKey),
                    () => Interlocked.Read(ref offsetHolder[0]));

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: finalizer);
        hc.BaseAddress = new Uri(CryptoExchanges.Net.Http.ExchangeUrl.NormalizeHostRoot(options.BaseUrl));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        return hc;
    }
}
