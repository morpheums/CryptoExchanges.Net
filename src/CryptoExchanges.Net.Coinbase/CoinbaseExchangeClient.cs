using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Coinbase.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Coinbase;

/// <summary>
/// Full Coinbase exchange client implementing <see cref="IExchangeClient"/>.
/// Composes market data, trading, and account services over the V3 brokerage REST API.
/// </summary>
public sealed class CoinbaseExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Internal composition constructor — called exclusively by <see cref="CoinbaseClientComposer"/>.
    /// Signing is owned by the resilience pipeline on the injected http client.
    /// </summary>
    internal CoinbaseExchangeClient(
        IMarketDataService marketData,
        IAccountService account,
        ITradingService trading)
    {
        MarketData = marketData;
        Account = account;
        Trading = trading;
    }

    /// <summary>
    /// Owner constructor — used by the container-free <see cref="Create"/> path to take lifecycle
    /// ownership of the underlying <see cref="HttpClient"/>.
    /// </summary>
    private CoinbaseExchangeClient(
        IMarketDataService marketData,
        IAccountService account,
        ITradingService trading,
        HttpClient httpClient)
        : this(marketData, account, trading)
    {
        _httpClient = httpClient;
        _ownsHttpClient = true;
    }

    /// <inheritdoc />
    public ExchangeId ExchangeId => ExchangeId.Coinbase;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>Creates a fully-wired client (container-free) for the given options.</summary>
    /// <param name="options">The Coinbase client options.</param>
    public static CoinbaseExchangeClient Create(CoinbaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        DelegatingHandler? signer = null;
        if (!string.IsNullOrEmpty(options.ApiKey) && !string.IsNullOrEmpty(options.PrivateKey))
            signer = new Resilience.CoinbaseSigningHandler(new Auth.CoinbaseJwtSigner(options.ApiKey, options.PrivateKey));

        var httpClient = CoinbaseClientComposer.BuildResilientHttpClient(options, signer);
        var coinbaseHttp = new CoinbaseHttpClient(httpClient);
        var symbolMapper = new SymbolMapper(CoinbaseSymbolFormat.Instance);
        var mapper = CoinbaseClientComposer.CreateMapper(symbolMapper);
        var (marketData, account, trading) = CoinbaseClientComposer.ComposeServices(coinbaseHttp, symbolMapper, mapper);
        return new CoinbaseExchangeClient(marketData, account, trading, httpClient);
    }

    /// <summary>Creates a client from COINBASE_API_KEY / COINBASE_PRIVATE_KEY environment variables.</summary>
    public static CoinbaseExchangeClient CreateFromEnvironment()
        => Create(new()
        {
            ApiKey = Environment.GetEnvironmentVariable("COINBASE_API_KEY") ?? string.Empty,
            PrivateKey = Environment.GetEnvironmentVariable("COINBASE_PRIVATE_KEY") ?? string.Empty
        });

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            // Coinbase time endpoint is public; reaching here without exception means the API is reachable.
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
