using System.Diagnostics.CodeAnalysis;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Http;

/// <summary>
/// Resolves keyed <see cref="IExchangeClient"/> singletons by <see cref="ExchangeId"/>.
/// Registered as a singleton; holds the provider and resolves on demand.
/// Lives in the shared Http assembly so each exchange's <c>AddXxxExchange</c> can register it
/// without depending on the aggregation package (see ADR-001).
/// </summary>
internal sealed class ExchangeClientFactory(IServiceProvider services) : IExchangeClientFactory
{
    // The registered set is fixed once the provider is built, so compute it once (lazily, to avoid
    // realizing every exchange client at factory-construction time) and reuse it.
    private readonly Lazy<IReadOnlyCollection<ExchangeId>> _available = new(() =>
        services.GetKeyedServices<IExchangeClient>(KeyedService.AnyKey)
            .Select(c => c.ExchangeId)
            .ToArray());

    /// <inheritdoc />
    public IReadOnlyCollection<ExchangeId> Available => _available.Value;

    /// <inheritdoc />
    public IExchangeClient GetClient(ExchangeId exchange)
        => services.GetKeyedService<IExchangeClient>(exchange)
            ?? throw new ExchangeNotRegisteredException(exchange);

    /// <inheritdoc />
    public bool TryGet(ExchangeId exchange, [NotNullWhen(true)] out IExchangeClient? client)
    {
        client = services.GetKeyedService<IExchangeClient>(exchange);
        return client is not null;
    }
}
