using System.Linq;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.DependencyInjection;

/// <summary>
/// Resolves keyed <see cref="IExchangeClient"/> singletons by <see cref="ExchangeId"/>.
/// Registered as a singleton; holds the provider and resolves on demand.
/// </summary>
internal sealed class ExchangeClientFactory(IServiceProvider services) : IExchangeClientFactory
{
    /// <inheritdoc />
    public IReadOnlyCollection<ExchangeId> Available
        => services.GetKeyedServices<IExchangeClient>(KeyedService.AnyKey)
            .Select(c => c.ExchangeId)
            .ToArray();

    /// <inheritdoc />
    public IExchangeClient GetClient(ExchangeId exchange)
        => services.GetKeyedService<IExchangeClient>(exchange)
            ?? throw new ExchangeNotRegisteredException(exchange);

    /// <inheritdoc />
    public bool TryGet(ExchangeId exchange, out IExchangeClient? client)
    {
        client = services.GetKeyedService<IExchangeClient>(exchange);
        return client is not null;
    }
}
