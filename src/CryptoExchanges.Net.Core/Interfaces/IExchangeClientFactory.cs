using System.Diagnostics.CodeAnalysis;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>
/// Exchange-agnostic entry point: resolves a registered <see cref="IExchangeClient"/> by
/// <see cref="ExchangeId"/> so consumer code depends only on this factory, not on keyed DI.
/// </summary>
public interface IExchangeClientFactory
{
    /// <summary>The exchanges that currently have a registered client.</summary>
    IReadOnlyCollection<ExchangeId> Available { get; }

    /// <summary>Gets the client for <paramref name="exchange"/>.</summary>
    /// <param name="exchange">The exchange to resolve.</param>
    /// <returns>The registered <see cref="IExchangeClient"/>.</returns>
    /// <exception cref="ExchangeNotRegisteredException">No client is registered for <paramref name="exchange"/>.</exception>
    IExchangeClient GetClient(ExchangeId exchange);

    /// <summary>Attempts to get the client for <paramref name="exchange"/> without throwing.</summary>
    /// <param name="exchange">The exchange to resolve.</param>
    /// <param name="client">The resolved client, or <see langword="null"/> when not registered.</param>
    /// <returns><see langword="true"/> if a client is registered for <paramref name="exchange"/>.</returns>
    bool TryGet(ExchangeId exchange, [NotNullWhen(true)] out IExchangeClient? client);
}
