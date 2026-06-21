using CryptoExchanges.Net.Kucoin.Streaming;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kucoin;

/// <summary>
/// Dependency-injection extensions for registering the KuCoin WebSocket streaming client.
/// Opt-in: REST-only consumers pay nothing for socket machinery.
/// </summary>
public static class StreamServiceCollectionExtensions
{
    internal const string KucoinClientName = "kucoin";

    /// <summary>
    /// Registers the KuCoin WebSocket streaming client and all its dependencies as per-exchange
    /// keyed singletons. Requires <c>AddKucoinExchange</c> to be called first so the keyed
    /// <c>ISymbolMapper</c> and <c>IMapper</c> are already available in the container. The
    /// bullet-public HTTP call reuses the named <c>kucoin</c> HttpClient (unauthenticated POST).
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional action to configure <see cref="KucoinStreamOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKucoinStreams(
        this IServiceCollection services,
        Action<KucoinStreamOptions>? configure = null) =>
        StreamServiceRegistration.AddStreams<KucoinStreamOptions>(
            services,
            ExchangeId.Kucoin,
            protocolFactory: sp =>
            {
                // Reuse the named kucoin HttpClient (already registered by AddKucoinExchange).
                // Bullet-public is unauthenticated (signed: false), so the signing handler is a no-op.
                var options = sp.GetRequiredService<KucoinStreamOptions>();
                // LR-001: guard the caller-configurable base URL before consuming it.
                ArgumentException.ThrowIfNullOrWhiteSpace(options.RestBaseUrl);
                if (!Uri.TryCreate(options.RestBaseUrl, UriKind.Absolute, out var baseUri))
                    throw new ArgumentException(
                        $"{nameof(KucoinStreamOptions)}.{nameof(KucoinStreamOptions.RestBaseUrl)} " +
                        $"must be a well-formed absolute URI. Got: '{options.RestBaseUrl}'.");

                var httpClientFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
                // CreateClient returns a fresh HttpClient per call, so overriding its host-only
                // BaseAddress here only affects the bullet-public negotiation, never the shared REST client.
                var httpClient = httpClientFactory.CreateClient(KucoinClientName);
                httpClient.BaseAddress = baseUri;
                IKucoinHttpClient http = new KucoinHttpClient(httpClient);
                var bulletClient = new KucoinBulletPublicClient(http);
                return new KucoinStreamProtocol(bulletClient);
            },
            decoderRegistryFactory: sp =>
            {
                var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Kucoin);
                var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Kucoin);
                return KucoinStreamDecoders.Build(mapper, symbolMapper);
            },
            configure: configure);
}
