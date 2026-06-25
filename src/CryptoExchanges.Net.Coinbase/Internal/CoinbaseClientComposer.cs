using CryptoExchanges.Net.Coinbase.Mapping;
using CryptoExchanges.Net.Coinbase.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Coinbase.Internal;

/// <summary>Single composition point for a Coinbase client — shared by the container-free Create path
/// and the DI registration. TASK-094 wires account/trading services additively via <see cref="ComposeServices"/>.</summary>
internal static class CoinbaseClientComposer
{
    /// <summary>Builds an <see cref="IMapper"/> from the Coinbase response profile bound to the given symbol mapper.</summary>
    public static IMapper CreateMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new CoinbaseResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    /// <summary>
    /// Builds all market-data-era services over the given http client. TASK-094 expands this with
    /// account and trading services when the full <c>CoinbaseExchangeClient</c> is added.
    /// </summary>
    public static CoinbaseMarketDataService ComposeServices(ICoinbaseHttpClient http, ISymbolMapper symbolMapper, IMapper mapper)
        => new(http, symbolMapper, mapper);

    /// <summary>DI composition: resolves the keyed symbol mapper + IMapper from the container and wires
    /// the services over the factory-owned http client (IHttpClientFactory owns the HttpClient lifetime).</summary>
    public static (CoinbaseMarketDataService marketData, ISymbolMapper symbolMapper, IMapper mapper) ComposeForDi(
        IServiceProvider sp, ICoinbaseHttpClient http)
    {
        ArgumentNullException.ThrowIfNull(sp);
        var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Coinbase);
        var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Coinbase);
        return (ComposeServices(http, symbolMapper, mapper), symbolMapper, mapper);
    }

    /// <summary>Builds the resilient HttpClient (factory-less path). A client missing the private key
    /// gets a no-op <see cref="PassThroughHandler"/> so public market-data works without credentials.</summary>
    public static HttpClient BuildResilientHttpClient(CoinbaseOptions options, DelegatingHandler? requestFinalizer = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        var resilienceOptions = new CryptoExchanges.Net.Core.Resilience.ResilienceOptions();
        var translator = new CryptoExchanges.Net.Coinbase.Resilience.CoinbaseErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();

        // A missing private key gets a no-op finalizer; full credentials are needed for signing.
        var finalizer = requestFinalizer ?? new PassThroughHandler();

        var hc = HttpClientPipelineBuilder.Build(inner, resilienceOptions, translator, gate, requestFinalizer: finalizer);
        // BaseAddress is host-only (no path) so RequestUri.PathAndQuery matches the JWT uri claim.
        hc.BaseAddress = new Uri(ExchangeUrl.NormalizeHostRoot(options.BaseUrl));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        return hc;
    }
}
