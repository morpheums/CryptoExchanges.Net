using System.Diagnostics.CodeAnalysis;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Full Binance exchange client implementing <see cref="IExchangeClient"/>.
/// Composes market data, trading, and account services.
/// </summary>
public sealed class BinanceExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;
    // CA1859 suppressed: interface type is intentional — enables typed-client DI and unit testing.
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "IBinanceHttpClient is held intentionally for DI / testability.")]
    private readonly IBinanceHttpClient _http;

    /// <summary>
    /// Clock-skew offset (in ms) shared with the signing handler. Single-element array so the
    /// handler's closure and <see cref="SyncServerTimeAsync"/> read/write the SAME instance.
    /// </summary>
    private readonly long[] _offsetHolder;
    private readonly Core.Resilience.IExchangeTimeSync _timeSync;

    /// <summary>
    /// Internal composition constructor — called exclusively by <see cref="Internal.BinanceClientComposer"/>.
    /// Signing and the API-key header are owned by the resilience pipeline on <paramref name="httpClient"/>,
    /// so this client needs no API key or signature service of its own.
    /// </summary>
    internal BinanceExchangeClient(
        IBinanceHttpClient http,
        IMarketDataService marketData,
        ITradingService trading,
        IAccountService account,
        bool ownsHttpClient,
        HttpClient? httpClient,
        long[] offsetHolder,
        Core.Resilience.IExchangeTimeSync timeSync)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(offsetHolder);
        ArgumentNullException.ThrowIfNull(timeSync);
        _http = http;
        MarketData = marketData;
        Trading = trading;
        Account = account;
        _ownsHttpClient = ownsHttpClient;
        _httpClient = httpClient;
        _offsetHolder = offsetHolder;
        _timeSync = timeSync;
    }

    /// <inheritdoc />
    public ExchangeId ExchangeId => ExchangeId.Binance;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>Creates a fully-wired client (container-free) for the given options.</summary>
    public static BinanceExchangeClient Create(BinanceOptions options)
        => Internal.BinanceClientComposer.Create(options);

    /// <summary>Creates a client from BINANCE_API_KEY / BINANCE_SECRET_KEY environment variables.</summary>
    public static BinanceExchangeClient CreateFromEnvironment()
        => Create(new()
        {
            ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? string.Empty,
            SecretKey = Environment.GetEnvironmentVariable("BINANCE_SECRET_KEY") ?? string.Empty
        });

    /// <summary>
    /// Syncs the clock-skew offset from Binance server time so signed requests don't trip -1021.
    /// Opt-in: call once after construction if the local clock may be skewed.
    /// </summary>
    public async Task SyncServerTimeAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync<ServerTimeDto>("/api/v3/time", signed: false, ct: ct).ConfigureAwait(false);
        // A missing/malformed /time payload (serverTime <= 0) is a degraded but non-fatal response:
        // skip the offset update (keep the prior/local clock) rather than throw out of SyncServerTimeAsync.
        if (resp.ServerTime > 0)
            _timeSync.ApplyOffset(
                resp.ServerTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            // The resilience pipeline throws typed exceptions on failure, so reaching here is success.
            _ = await _http.GetAsync<ServerTimeDto>("/api/v3/time", signed: false, ct: ct).ConfigureAwait(false);
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
