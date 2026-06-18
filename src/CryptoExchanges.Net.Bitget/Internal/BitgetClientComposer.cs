using CryptoExchanges.Net.Bitget.Auth;
using CryptoExchanges.Net.Bitget.Mapping;
using CryptoExchanges.Net.Bitget.Resilience;
using CryptoExchanges.Net.Bitget.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bitget.Internal;

/// <summary>Single composition point for a Bitget client — shared by the container-free
/// <see cref="BitgetExchangeClient.Create"/> path and the DI registration.</summary>
internal static class BitgetClientComposer
{
    /// <summary>Builds an IMapper from the Bitget response profile bound to the given symbol mapper.</summary>
    public static IMapper CreateMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new BitgetResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    /// <summary>Container-free composition: builds the resilient HttpClient and the fully-wired client.</summary>
    public static BitgetExchangeClient Create(BitgetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var offsetHolder = new long[] { 0L };
        var hc = BuildResilientHttpClient(options, offsetHolder);
        var http = new BitgetHttpClient(hc);
        return ComposeOver(http, hc, ownsHttpClient: true, offsetHolder, new ExchangeTimeSync());
    }

    /// <summary>Wires symbol mapper + IMapper + the 3 services + the client over a given http client.</summary>
    public static BitgetExchangeClient ComposeOver(
        IBitgetHttpClient http, HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder,
        IExchangeTimeSync timeSync)
    {
        ISymbolMapper symbolMapper = new SymbolMapper(BitgetSymbolFormat.Instance);
        return ComposeWith(http, symbolMapper, CreateMapper(symbolMapper), httpClient, ownsHttpClient, offsetHolder, timeSync);
    }

    /// <summary>Wires the 3 services + client using already-resolved symbol mapper + IMapper (DI path).</summary>
    public static BitgetExchangeClient ComposeWith(
        IBitgetHttpClient http, ISymbolMapper symbolMapper, IMapper mapper,
        HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder, IExchangeTimeSync timeSync)
    {
        var market = new BitgetMarketDataService(http, symbolMapper, mapper);
        var trading = new BitgetTradingService(http, symbolMapper, mapper);
        var account = new BitgetAccountService(http, symbolMapper, mapper);
        return new BitgetExchangeClient(http, market, trading, account, ownsHttpClient, httpClient, offsetHolder, timeSync);
    }

    /// <summary>DI composition: resolves the keyed symbol mapper + IMapper from the container and wires
    /// the services + client over the factory-owned http client (ownsHttpClient: false — IHttpClientFactory
    /// owns the underlying HttpClient lifetime).</summary>
    public static BitgetExchangeClient ComposeForDi(IServiceProvider sp, IBitgetHttpClient http, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(sp);
        var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Bitget);
        var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Bitget);
        var timeSync = sp.GetRequiredService<IExchangeTimeSync>();
        // INVARIANT: ownsHttpClient MUST stay false — IHttpClientFactory owns the HttpClient and its
        // handler chain; this client must NOT dispose it. Flipping this would double-dispose factory state.
        return ComposeWith(http, symbolMapper, mapper, httpClient: null, ownsHttpClient: false, offsetHolder, timeSync);
    }

    /// <summary>Builds the resilient HttpClient (factory-less path) with a secret+passphrase-gated signing
    /// finalizer over the shared offset holder. A client missing the secret OR the passphrase gets a no-op
    /// PassThrough finalizer so public market-data works without full credentials (mirrors the DI gate).</summary>
    public static HttpClient BuildResilientHttpClient(BitgetOptions options, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(offsetHolder);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        var resilienceOptions = new CryptoExchanges.Net.Core.Resilience.ResilienceOptions();
        var translator = new BitgetErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();

        // Options are final here, so the gate is decided now. Bitget signing needs BOTH the secret AND
        // the passphrase; if either is missing the client gets a no-op PassThroughHandler. Because the
        // gate requires both, the secret/passphrase are passed to BitgetSigningHandler directly — no
        // BitgetOptions.ToCredentials() (which throws on an empty passphrase) is needed in the signing path.
        DelegatingHandler finalizer =
            (string.IsNullOrEmpty(options.SecretKey) || string.IsNullOrEmpty(options.Passphrase))
                ? new CryptoExchanges.Net.Http.PassThroughHandler()
                : new BitgetSigningHandler(
                    options.ApiKey,
                    options.Passphrase,
                    new BitgetSignatureService(options.SecretKey),
                    () => Interlocked.Read(ref offsetHolder[0]));

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: finalizer);
        // BaseAddress is host-only (no path) so RequestUri.AbsolutePath == the Bitget requestPath and
        // RequestUri.Query == the signed query string (the prehash invariant from TASK-021). The shared
        // ExchangeUrl.NormalizeHostRoot guard keeps sign-consistency self-enforcing on this path too.
        hc.BaseAddress = new Uri(CryptoExchanges.Net.Http.ExchangeUrl.NormalizeHostRoot(options.BaseUrl));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        return hc;
    }
}
