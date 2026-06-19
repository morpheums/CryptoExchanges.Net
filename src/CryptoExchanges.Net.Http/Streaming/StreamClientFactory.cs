using System.Diagnostics.CodeAnalysis;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Resolves keyed <see cref="IStreamClient"/> singletons by <see cref="ExchangeId"/>.
/// Registered as a singleton; holds the provider and resolves on demand.
/// Lives in the shared Http assembly so each exchange's <c>AddXxxStreams</c> can register
/// it without depending on the aggregation package (mirrors <c>ExchangeClientFactory</c>,
/// see ADR-001).
/// </summary>
internal sealed class StreamClientFactory(IServiceProvider services) : IStreamClientFactory
{
    // The registered set is fixed once the provider is built, so compute it once (lazily, to avoid
    // realizing every stream client at factory-construction time) and reuse it.
    private readonly Lazy<IReadOnlyCollection<ExchangeId>> _available = new(() =>
        services.GetKeyedServices<IStreamClient>(KeyedService.AnyKey)
            .Select(c => c.ExchangeId)
            .ToArray());

    /// <inheritdoc />
    public IReadOnlyCollection<ExchangeId> Available => _available.Value;

    /// <inheritdoc />
    public IStreamClient GetClient(ExchangeId exchange)
        => services.GetKeyedService<IStreamClient>(exchange)
            ?? throw new ExchangeNotRegisteredException(exchange);

    /// <inheritdoc />
    public bool TryGet(ExchangeId exchange, [NotNullWhen(true)] out IStreamClient? client)
    {
        client = services.GetKeyedService<IStreamClient>(exchange);
        return client is not null;
    }

    // ── Container-free path (mirrors ExchangeClientFactory) ──────────────────

    /// <summary>
    /// Container-free factory method: builds a fully-wired <see cref="StreamClient"/>
    /// without a DI container. Equivalent composition to the DI path.
    /// </summary>
    /// <param name="exchangeId">The exchange this client represents.</param>
    /// <param name="protocol">The venue-specific protocol strategy.</param>
    /// <param name="decoders">The per-stream-kind decode closure registry.</param>
    /// <param name="options">Engine configuration (channel capacity, backoff, idle-close).</param>
    /// <param name="connectionFactory">
    /// Factory that produces a fresh <see cref="IWebSocketConnection"/> for each connect/reconnect.
    /// </param>
    /// <param name="logger">Logger for engine events; pass <see cref="NullLogger.Instance"/> in tests.</param>
    /// <param name="symbolMapper">Resolves canonical <see cref="Core.Models.Symbol"/> to exchange wire strings.</param>
    /// <returns>A fully-wired <see cref="IStreamClient"/>.</returns>
    public static IStreamClient Create(
        ExchangeId exchangeId,
        IStreamProtocol protocol,
        StreamDecoderRegistry decoders,
        StreamEngineOptions options,
        Func<IWebSocketConnection> connectionFactory,
        ILogger logger,
        ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(decoders);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        // Ownership of the engine transfers to StreamClient on success;
        // on exception, the engine is async-disposed inline.
        // CA2000: intentional — ownership transfer to StreamClient; false-positive for IAsyncDisposable.
#pragma warning disable CA2000
        var engine = new StreamEngine(protocol, decoders, options, connectionFactory, logger);
#pragma warning restore CA2000
        try
        {
            return new StreamClient(engine, symbolMapper, exchangeId);
        }
        catch
        {
            engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }
}
