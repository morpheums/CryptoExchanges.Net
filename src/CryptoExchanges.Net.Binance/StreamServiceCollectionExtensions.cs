using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Binance.Streaming;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Dependency-injection extensions for registering the venue WebSocket streaming client.
/// Opt-in: REST-only consumers pay nothing for socket machinery.
/// </summary>
public static class StreamServiceCollectionExtensions
{
    /// <summary>
    /// Registers the venue WebSocket streaming client and all its dependencies as per-exchange
    /// keyed singletons. Requires <c>AddBinanceExchange</c> to be called first so the keyed
    /// <c>ISymbolMapper</c> and <c>IMapper</c> are already available in the container.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional action to configure <see cref="BinanceStreamOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBinanceStreams(
        this IServiceCollection services,
        Action<BinanceStreamOptions>? configure = null) =>
        StreamServiceRegistration.AddStreams<BinanceStreamOptions>(
            services,
            ExchangeId.Binance,
            protocolFactory: sp =>
            {
                var opts = sp.GetRequiredService<BinanceStreamOptions>();
                return new BinanceStreamProtocol(opts);
            },
            decoderRegistryFactory: sp =>
            {
                var mapper = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Binance);
                var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Binance);
                return BinanceStreamDecoders.Build(mapper, symbolMapper);
            },
            configure: configure);
}
