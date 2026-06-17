using CryptoExchanges.Net.Binance.Mapping;
using CryptoExchanges.Net.Binance.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Binance.Internal;

/// <summary>Single composition point for a Binance client — shared by the container-free
/// <see cref="BinanceExchangeClient.Create"/> path and the DI registration.</summary>
internal static class BinanceClientComposer
{
    /// <summary>Builds an IMapper from the Binance response profile bound to the given symbol mapper.</summary>
    public static IMapper CreateMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new BinanceResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    /// <summary>Container-free composition: builds the resilient HttpClient and the fully-wired client.</summary>
    public static BinanceExchangeClient Create(BinanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var offsetHolder = new long[] { 0L };
        var hc = BuildResilientHttpClient(options, offsetHolder);
        var http = new BinanceHttpClient(hc, options);
        return ComposeOver(http, hc, ownsHttpClient: true, offsetHolder);
    }

    /// <summary>Wires symbol mapper + IMapper + the 3 services + the client over a given http client.</summary>
    public static BinanceExchangeClient ComposeOver(
        IBinanceHttpClient http, HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder)
    {
        ISymbolMapper symbolMapper = new SymbolMapper(BinanceSymbolFormat.Instance);
        return ComposeWith(http, symbolMapper, CreateMapper(symbolMapper), httpClient, ownsHttpClient, offsetHolder);
    }

    /// <summary>Wires the 3 services + client using already-resolved symbol mapper + IMapper (DI path).</summary>
    public static BinanceExchangeClient ComposeWith(
        IBinanceHttpClient http, ISymbolMapper symbolMapper, IMapper mapper,
        HttpClient? httpClient, bool ownsHttpClient, long[] offsetHolder)
    {
        var market = new BinanceMarketDataService(http, symbolMapper, mapper);
        var trading = new BinanceTradingService(http, symbolMapper, mapper);
        var account = new BinanceAccountService(http, symbolMapper, mapper);
        return new BinanceExchangeClient(http, market, trading, account, ownsHttpClient, httpClient, offsetHolder);
    }

    /// <summary>Builds the resilient HttpClient (factory-less path) with a secret-gated signing finalizer
    /// over the shared offset holder.</summary>
    public static HttpClient BuildResilientHttpClient(BinanceOptions options, long[] offsetHolder)
    {
        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        BinanceSignatureService? sig = string.IsNullOrEmpty(options.SecretKey) ? null : new(options.SecretKey);
        var resilienceOptions = new CryptoExchanges.Net.Core.Resilience.ResilienceOptions { UsageHeaderName = "X-MBX-USED-WEIGHT-1m" };
        var translator = new BinanceErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();
        BinanceSigningHandler? signing = sig is null
            ? null
            : new BinanceSigningHandler(options.ApiKey, sig, () => Interlocked.Read(ref offsetHolder[0]));

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: signing);
        hc.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        if (!string.IsNullOrEmpty(options.ApiKey))
            hc.DefaultRequestHeaders.Add("X-MBX-APIKEY", options.ApiKey);
        return hc;
    }
}
