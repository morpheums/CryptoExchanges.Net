using CryptoExchanges.Net.Coinbase.Auth;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Coinbase.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CryptoExchanges.Net.Coinbase;

/// <summary>
/// Dependency-injection extensions for registering the Coinbase Advanced Trade exchange client.
/// Lives in the Coinbase assembly so a consumer can depend on Coinbase alone without transitively
/// pulling in other exchange assemblies (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string CoinbaseClientName = "coinbase";

    /// <summary>
    /// Registers the Coinbase exchange client's keyed symbol mapper, DeltaMapper, and REST HTTP
    /// pipeline as per-exchange keyed singletons. Options are validated with fail-fast
    /// (<c>ValidateOnStart</c>). Call this before <c>AddCoinbaseStreams</c>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional action to configure <see cref="CoinbaseOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCoinbaseExchange(
        this IServiceCollection services,
        Action<CoinbaseOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<CoinbaseOptions>()
            .Configure(o =>
            {
                var k = Environment.GetEnvironmentVariable("COINBASE_API_KEY");
                var p = Environment.GetEnvironmentVariable("COINBASE_PRIVATE_KEY");
                if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
                if (!string.IsNullOrEmpty(p)) o.PrivateKey = p;
            })
            .Configure(o => configure?.Invoke(o))
            .Validate(o => o.TimeoutSeconds > 0, "CoinbaseOptions.TimeoutSeconds must be > 0.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "CoinbaseOptions.BaseUrl is required.")
            .ValidateOnStart();

        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<CoinbaseOptions>>().Value);

        services.TryAddKeyedSingleton<ISymbolMapper>(ExchangeId.Coinbase,
            (_, _) => (ISymbolMapper)new SymbolMapper(CoinbaseSymbolFormat.Instance));

        services.TryAddKeyedSingleton<IMapper>(ExchangeId.Coinbase, (sp, _) =>
        {
            var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Coinbase);
            return CoinbaseClientComposer.CreateMapper(symbolMapper);
        });

        services.AddHttpClient(CoinbaseClientName, (sp, c) =>
            {
                var o = sp.GetRequiredService<CoinbaseOptions>();
                c.BaseAddress = new Uri(ExchangeUrl.NormalizeHostRoot(o.BaseUrl));
                c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
                c.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
            .ApplyResiliencePipeline(
                CoinbaseClientName,
                new ResilienceOptions(),
                translatorFactory: _ => new CoinbaseErrorTranslator(),
                gateFactory: _ => new ReactiveRateLimitGate(),
                requestFinalizerFactory: sp =>
                {
                    var o = sp.GetRequiredService<CoinbaseOptions>();
                    if (string.IsNullOrEmpty(o.ApiKey) || string.IsNullOrEmpty(o.PrivateKey))
                        return new PassThroughHandler();
                    return new CoinbaseSigningHandler(new CoinbaseJwtSigner(o.ApiKey, o.PrivateKey));
                });

        return services;
    }
}
