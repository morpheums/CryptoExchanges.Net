using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Configuration options for the Binance exchange client.
/// </summary>
public sealed class BinanceOptions
{
    /// <summary>The Binance REST API base URL. Default: https://api.binance.com</summary>
    public string BaseUrl { get; set; } = "https://api.binance.com";

    /// <summary>Binance API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Binance API secret key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Receive window in milliseconds (decimal). Default: 5000.</summary>
    public decimal ReceiveWindow { get; set; } = 5000m;
}

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
        long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(offsetHolder);
        _http = http;
        MarketData = marketData;
        Trading = trading;
        Account = account;
        _ownsHttpClient = ownsHttpClient;
        _httpClient = httpClient;
        _offsetHolder = offsetHolder;
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
        var resp = await _http.GetAsync<BinanceServerTimeResponse>("/api/v3/time", signed: false, ct: ct).ConfigureAwait(false);
        var offset = Resilience.BinanceTimeSync.ComputeOffset(
            resp.ServerTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Interlocked.Exchange(ref _offsetHolder[0], offset);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            // The resilience pipeline throws typed exceptions on failure, so reaching here is success.
            _ = await _http.GetAsync<BinanceServerTimeResponse>("/api/v3/time", signed: false, ct: ct).ConfigureAwait(false);
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

/// <summary>
/// Simple DTO for the /api/v3/time response used by PingAsync.
/// </summary>
internal sealed record BinanceServerTimeResponse
{
    [JsonPropertyName("serverTime")]
    public long ServerTime { get; init; }
}
