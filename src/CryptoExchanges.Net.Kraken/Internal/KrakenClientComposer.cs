using CryptoExchanges.Net.Kraken.Mapping;
using CryptoExchanges.Net.Kraken.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Resilience;
using DeltaMapper;

namespace CryptoExchanges.Net.Kraken.Internal;

/// <summary>Single composition point for a Kraken client — shared by the container-free factory path
/// and the DI registration. TASK-101 adds account and trading services additively here.</summary>
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

    /// <summary>Builds a resilient HttpClient (factory-less path). The signing handler is wired by
    /// TASK-101; for now a PassThroughHandler is used so public market-data works without credentials.</summary>
    public static HttpClient BuildResilientHttpClient(KrakenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        var resilienceOptions = new ResilienceOptions();
        var translator = new CryptoExchanges.Net.Kraken.Resilience.KrakenErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();

        // Signing handler (TASK-101) will replace PassThroughHandler when credentials are available.
        DelegatingHandler finalizer = new CryptoExchanges.Net.Http.PassThroughHandler();

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: finalizer);
        hc.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        return hc;
    }
}
