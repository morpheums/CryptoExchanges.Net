using CryptoExchanges.Net.Kraken.Auth;
using CryptoExchanges.Net.Kraken.Mapping;
using CryptoExchanges.Net.Kraken.Resilience;
using CryptoExchanges.Net.Kraken.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kraken.Internal;

/// <summary>Single composition point for a Kraken client — shared by the container-free factory path
/// and the DI registration.</summary>
internal static class KrakenClientComposer
{
    /// <summary>Builds an IMapper from the Kraken response profile bound to the given symbol mapper.</summary>
    public static IMapper CreateMapper(ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(symbolMapper);
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new KrakenResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    /// <summary>Container-free composition: builds the resilient HttpClient and the fully-wired client.</summary>
    public static KrakenExchangeClient Create(KrakenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var hc = BuildResilientHttpClient(options);
        var symbolMapper = new SymbolMapper(KrakenSymbolFormat.Instance);
        var mapper = CreateMapper(symbolMapper);
        var http = new KrakenHttpClient(hc, symbolMapper);
        return ComposeWith(http, symbolMapper, mapper, ownsHttpClient: true, httpClient: hc);
    }

    /// <summary>Wires the three services + client using already-resolved symbol mapper and IMapper.</summary>
    public static KrakenExchangeClient ComposeWith(
        KrakenHttpClient http,
        ISymbolMapper symbolMapper,
        IMapper mapper,
        bool ownsHttpClient,
        HttpClient? httpClient)
    {
        var market  = new KrakenMarketDataService(http, symbolMapper, mapper);
        var trading = new KrakenTradingService(http, symbolMapper, mapper);
        var account = new KrakenAccountService(http, symbolMapper, mapper);
        return new KrakenExchangeClient(market, trading, account, ownsHttpClient, httpClient);
    }

    /// <summary>DI composition: resolves the keyed symbol mapper + IMapper from the container and wires
    /// the services + client over the factory-owned http client.</summary>
    public static KrakenExchangeClient ComposeForDi(IServiceProvider sp, HttpClient httpClient, long[] _)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(httpClient);
        var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Kraken);
        var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Kraken);
        var http = new KrakenHttpClient(httpClient, symbolMapper);
        return ComposeWith(http, symbolMapper, mapper, ownsHttpClient: false, httpClient: null);
    }

    /// <summary>Builds the Kraken HTTP client and market-data service over the given HttpClient.</summary>
    public static (KrakenHttpClient http, KrakenMarketDataService market) ComposeMarketData(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ISymbolMapper symbolMapper = new SymbolMapper(KrakenSymbolFormat.Instance);
        var http = new KrakenHttpClient(httpClient, symbolMapper);
        var mapper = CreateMapper(symbolMapper);
        var market = new KrakenMarketDataService(http, symbolMapper, mapper);
        return (http, market);
    }

    /// <summary>Builds a resilient HttpClient (factory-less path). The signing handler is wired when
    /// credentials are present; a PassThroughHandler is used when they are absent.</summary>
    public static HttpClient BuildResilientHttpClient(KrakenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        var resilienceOptions = new ResilienceOptions();
        var translator = new KrakenErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();

        DelegatingHandler finalizer = string.IsNullOrEmpty(options.ApiKey) || string.IsNullOrEmpty(options.ApiSecret)
            ? new CryptoExchanges.Net.Http.PassThroughHandler()
            : new KrakenSigningHandler(options.ApiKey, new KrakenSignatureService(options.ApiSecret));

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: finalizer);
        hc.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        return hc;
    }
}
