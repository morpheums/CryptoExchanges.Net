using CryptoExchanges.Net.Okx.Auth;
using CryptoExchanges.Net.Okx.Mapping;
using CryptoExchanges.Net.Okx.Resilience;
using CryptoExchanges.Net.Okx.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Okx.Internal;

/// <summary>Single composition point for an OKX client — shared by the container-free
/// <see cref="OkxExchangeClient.Create"/> path and the DI registration.</summary>
internal static class OkxClientComposer
{
    /// <summary>Builds an IMapper from the OKX response profile bound to the given symbol mapper.</summary>
    public static IMapper CreateMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new OkxResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    /// <summary>Container-free composition: builds the resilient HttpClient and the fully-wired client.</summary>
    public static OkxExchangeClient Create(OkxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var offsetHolder = new long[] { 0L };
        var hc = BuildResilientHttpClient(options, offsetHolder);
        var http = new OkxHttpClient(hc);
        return ComposeOver(http, hc, ownsHttpClient: true, offsetHolder, new ExchangeTimeSync());
    }

    /// <summary>Wires symbol mapper + IMapper + the 3 services + the client over a given http client.</summary>
    public static OkxExchangeClient ComposeOver(
        IOkxHttpClient http, HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder,
        IExchangeTimeSync timeSync)
    {
        ISymbolMapper symbolMapper = new SymbolMapper(OkxSymbolFormat.Instance);
        return ComposeWith(http, symbolMapper, CreateMapper(symbolMapper), httpClient, ownsHttpClient, offsetHolder, timeSync);
    }

    /// <summary>Wires the 3 services + client using already-resolved symbol mapper + IMapper (DI path).</summary>
    public static OkxExchangeClient ComposeWith(
        IOkxHttpClient http, ISymbolMapper symbolMapper, IMapper mapper,
        HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder, IExchangeTimeSync timeSync)
    {
        var market = new OkxMarketDataService(http, symbolMapper, mapper);
        var trading = new OkxTradingService(http, symbolMapper, mapper);
        var account = new OkxAccountService(http, symbolMapper, mapper);
        return new OkxExchangeClient(http, market, trading, account, ownsHttpClient, httpClient, offsetHolder, timeSync);
    }

    /// <summary>DI composition: resolves the keyed symbol mapper + IMapper from the container and wires
    /// the services + client over the factory-owned http client (ownsHttpClient: false — IHttpClientFactory
    /// owns the underlying HttpClient lifetime).</summary>
    public static OkxExchangeClient ComposeForDi(IServiceProvider sp, IOkxHttpClient http, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(sp);
        var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Okx);
        var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Okx);
        var timeSync = sp.GetRequiredService<IExchangeTimeSync>();
        // INVARIANT: ownsHttpClient MUST stay false — IHttpClientFactory owns the HttpClient and its
        // handler chain; this client must NOT dispose it. Flipping this would double-dispose factory state.
        return ComposeWith(http, symbolMapper, mapper, httpClient: null, ownsHttpClient: false, offsetHolder, timeSync);
    }

    /// <summary>Builds the resilient HttpClient (factory-less path) with a secret+passphrase-gated signing
    /// finalizer over the shared offset holder. A client missing the secret OR the passphrase gets a no-op
    /// PassThrough finalizer so public market-data works without full credentials (mirrors the DI gate).</summary>
    public static HttpClient BuildResilientHttpClient(OkxOptions options, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(offsetHolder);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        var resilienceOptions = new CryptoExchanges.Net.Core.Resilience.ResilienceOptions();
        var translator = new OkxErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();

        // Options are final here, so the gate is decided now. OKX signing needs BOTH the secret AND the
        // passphrase; if either is missing the client gets a no-op PassThroughHandler. Because the gate
        // requires both, the secret/passphrase are passed to OkxSigningHandler directly — no
        // OkxOptions.ToCredentials() (which throws on an empty passphrase) is needed in the signing path.
        DelegatingHandler finalizer =
            (string.IsNullOrEmpty(options.SecretKey) || string.IsNullOrEmpty(options.Passphrase))
                ? new CryptoExchanges.Net.Http.PassThroughHandler()
                : new OkxSigningHandler(
                    options.ApiKey,
                    options.Passphrase,
                    new OkxSignatureService(options.SecretKey),
                    () => Interlocked.Read(ref offsetHolder[0]));

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: finalizer);
        // BaseAddress is host-only (no path) so RequestUri.PathAndQuery == the OKX requestPath that gets
        // signed (the prehash invariant from TASK-014).
        hc.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        return hc;
    }
}
