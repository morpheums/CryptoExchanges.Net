using CryptoExchanges.Net.Bybit.Streaming;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bybit;

/// <summary>
/// Dependency-injection extensions for registering the Bybit venue WebSocket streaming client.
/// Opt-in: REST-only consumers pay nothing for socket machinery.
/// </summary>
public static class StreamServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Bybit venue WebSocket streaming client and all its dependencies as
    /// per-exchange keyed singletons. Requires <c>AddBybitExchange</c> to be called first so
    /// the keyed <c>ISymbolMapper</c> and <c>IMapper</c> are already available in the container.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional action to configure <see cref="StreamOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBybitStreams(
        this IServiceCollection services,
        Action<StreamOptions>? configure = null) =>
        StreamServiceRegistration.AddStreams<StreamOptions>(
            services,
            ExchangeId.Bybit,
            protocolFactory: sp =>
            {
                var opts = sp.GetRequiredService<StreamOptions>();
                return new BybitStreamProtocol(opts);
            },
            decoderRegistryFactory: sp =>
            {
                var mapper = sp.GetKeyedService<IMapper>(ExchangeId.Bybit)
                    ?? throw new InvalidOperationException(
                        "No keyed IMapper registered for 'Bybit'. Call AddBybitExchange before AddBybitStreams.");
                var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Bybit);
                return BybitStreamDecoders.Build(mapper, symbolMapper);
            },
            configure: configure);
}
