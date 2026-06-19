using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Shared body of every <c>AddXxxStreams</c> registration; each exchange supplies only
/// its variation points (<see cref="IStreamProtocol"/> factory + decode-registry factory).
/// Mirrors the grain of <c>ExchangeServiceRegistration.AddExchange&lt;TOptions,TMapper&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Binding constraint K1: this class never references <c>Core.Models</c> types or DeltaMapper.
/// The decode-registry factory is an opaque <see cref="Func{T, TResult}"/> supplied by the
/// exchange package; Http only holds the resulting <see cref="StreamDecoderRegistry"/> of
/// opaque <c>Func&lt;ReadOnlyMemory&lt;byte&gt;, object&gt;</c> closures.
/// </para>
/// <para>
/// No captive dependency (Inv 9): the long-lived <see cref="StreamEngine"/> + transport are
/// owned <em>inside</em> the keyed <see cref="IStreamClient"/> singleton. The engine's
/// connection factory produces a fresh <see cref="IWebSocketConnection"/> per connect attempt,
/// so the singleton does not capture a transient.
/// </para>
/// </remarks>
internal static class StreamServiceRegistration
{
    /// <summary>
    /// Registers a streaming client and all its dependencies as per-exchange keyed singletons.
    /// </summary>
    /// <typeparam name="TOptions">The per-exchange stream options type.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="exchangeId">The per-exchange key for all keyed singletons.</param>
    /// <param name="protocolFactory">
    /// Builds the per-exchange <see cref="IStreamProtocol"/>. The factory receives the
    /// <see cref="IServiceProvider"/> so it can resolve its own keyed options/credentials.
    /// </param>
    /// <param name="decoderRegistryFactory">
    /// Builds the per-exchange <see cref="StreamDecoderRegistry"/> of opaque decode closures.
    /// The factory receives the <see cref="IServiceProvider"/> so it can resolve the keyed
    /// <c>ISymbolMapper</c>/<c>IMapper</c> already registered by <c>AddXxxExchange</c>.
    /// Http never sees the closure internals (K1).
    /// </param>
    /// <param name="configure">
    /// Optional per-exchange options configuration applied on top of any environment defaults.
    /// </param>
    /// <param name="connectionFactory">
    /// Optional factory that produces a fresh <see cref="IWebSocketConnection"/> for each
    /// connect/reconnect attempt. When <see langword="null"/> a default
    /// <see cref="ClientWebSocketConnection"/> factory is used.
    /// </param>
    /// <param name="engineOptionsFactory">
    /// Optional factory for per-exchange <see cref="StreamEngineOptions"/> (channel capacity,
    /// backoff, idle-close window). When <see langword="null"/> sensible defaults are used.
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddStreams<TOptions>(
        IServiceCollection services,
        ExchangeId exchangeId,
        Func<IServiceProvider, IStreamProtocol> protocolFactory,
        Func<IServiceProvider, StreamDecoderRegistry> decoderRegistryFactory,
        Action<TOptions>? configure,
        Func<IServiceProvider, Func<IWebSocketConnection>>? connectionFactory = null,
        Func<IServiceProvider, StreamEngineOptions>? engineOptionsFactory = null)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(protocolFactory);
        ArgumentNullException.ThrowIfNull(decoderRegistryFactory);

        // Register the shared factory once (TryAdd — first registration wins).
        services.TryAddSingleton<IStreamClientFactory, StreamClientFactory>();

        // Per-exchange options with ValidateOnStart.
        services.AddOptions<TOptions>()
            .Configure(o => configure?.Invoke(o))
            .ValidateOnStart();

        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<TOptions>>().Value);

        // Register keyed ISymbolMapper if not already registered by AddXxxExchange.
        // TryAdd ensures the exchange's existing keyed mapper is reused (not replaced).
        // The stream client resolves it from the container rather than constructing a new one.
        services.TryAddKeyedSingleton<ISymbolMapper>(exchangeId, (_, _) =>
            throw new InvalidOperationException(
                $"No keyed ISymbolMapper registered for '{exchangeId}'. " +
                $"Call AddXxxExchange before AddXxxStreams, or register the mapper manually."));

        // The IStreamClient keyed singleton: owns the engine + transport (no captive dependency,
        // Inv 9 — the connection factory produces a fresh socket per connect attempt).
        services.AddKeyedSingleton<IStreamClient>(exchangeId, (sp, _) =>
        {
            var protocol = protocolFactory(sp);
            var decoders = decoderRegistryFactory(sp);
            var engineOpts = engineOptionsFactory?.Invoke(sp) ?? new StreamEngineOptions();

            Func<IWebSocketConnection> connFactory = connectionFactory is not null
                ? connectionFactory(sp)
                : () => new ClientWebSocketConnection();

            var loggerFactory = sp.GetService<ILoggerFactory>();
            ILogger logger = loggerFactory is not null
                ? loggerFactory.CreateLogger<StreamClient>()
                : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            var engine = new StreamEngine(protocol, decoders, engineOpts, connFactory, logger);
            var symbolMapper = sp.GetRequiredKeyedService<ISymbolMapper>(exchangeId);
            return new StreamClient(engine, symbolMapper, exchangeId);
        });

        return services;
    }
}
