using CryptoExchanges.Net.Kraken.Services;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Kraken;

/// <summary>
/// Full Kraken exchange client implementing <see cref="IExchangeClient"/>.
/// Composes market data, trading, and account services over the Spot REST API.
/// </summary>
public sealed class KrakenExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Internal composition constructor — called exclusively by <see cref="Internal.KrakenClientComposer"/>.
    /// Signing and credential headers are owned by the resilience pipeline on
    /// <paramref name="httpClient"/>, so this client needs no API key or secret of its own.
    /// </summary>
    internal KrakenExchangeClient(
        IMarketDataService marketData,
        ITradingService trading,
        IAccountService account,
        bool ownsHttpClient,
        HttpClient? httpClient)
    {
        MarketData = marketData;
        Trading = trading;
        Account = account;
        _ownsHttpClient = ownsHttpClient;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public ExchangeId ExchangeId => ExchangeId.Kraken;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>Creates a fully-wired client (container-free) for the given options.</summary>
    /// <param name="options">The Kraken client options.</param>
    public static KrakenExchangeClient Create(KrakenOptions options)
        => Internal.KrakenClientComposer.Create(options);

    /// <summary>Creates a client from KRAKEN_API_KEY / KRAKEN_API_SECRET environment variables.</summary>
    public static KrakenExchangeClient CreateFromEnvironment()
        => Create(new()
        {
            ApiKey = Environment.GetEnvironmentVariable("KRAKEN_API_KEY") ?? string.Empty,
            ApiSecret = Environment.GetEnvironmentVariable("KRAKEN_API_SECRET") ?? string.Empty
        });

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            _ = await MarketData.GetExchangeInfoAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (CryptoExchanges.Net.Core.Exceptions.ExchangeException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsHttpClient && _httpClient is not null)
            _httpClient.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
