using CryptoExchanges.Net.Kraken.Streaming;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kraken;

/// <summary>
/// Dependency-injection extensions for registering the Kraken venue WebSocket streaming client.
/// Opt-in: REST-only consumers pay nothing for socket machinery.
/// </summary>
public static class StreamServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kraken venue WebSocket streaming client and all its dependencies as
    /// per-exchange keyed singletons. Requires <c>AddKrakenExchange</c> to be called first so
    /// the keyed <c>ISymbolMapper</c> and <c>IMapper</c> are already available in the container.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional action to configure <see cref="StreamOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKrakenStreams(
        this IServiceCollection services,
        Action<StreamOptions>? configure = null) =>
        StreamServiceRegistration.AddStreams<StreamOptions>(
            services,
            ExchangeId.Kraken,
            protocolFactory: sp =>
            {
                var opts = sp.GetRequiredService<StreamOptions>();
                return new KrakenStreamProtocol(opts);
            },
            decoderRegistryFactory: sp =>
            {
                var mapper = sp.GetKeyedService<IMapper>(ExchangeId.Kraken)
                    ?? throw new InvalidOperationException(
                        "No keyed IMapper registered for 'Kraken'. Call AddKrakenExchange before AddKrakenStreams.");
                var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Kraken);
                return KrakenStreamDecoders.Build(mapper, symbolMapper);
            },
            configure: configure);
}
