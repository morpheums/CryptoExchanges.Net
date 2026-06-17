using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CryptoExchanges.Net.Binance.Mapping;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using DeltaMapper;

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
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly BinanceHttpClient _http;

    /// <summary>
    /// Clock-skew offset (in ms) shared with the signing handler. Single-element array so the
    /// handler's closure and <see cref="SyncServerTimeAsync"/> read/write the SAME instance.
    /// This default applies to externally-constructed clients; <see cref="Create"/> replaces it
    /// with the shared array instance the pipeline's signing handler closes over.
    /// </summary>
    private long[] _offsetHolder = [0L];

    /// <summary>
    /// Initializes a new <see cref="BinanceExchangeClient"/>. Signing and the API-key header are
    /// owned by the resilience pipeline on <paramref name="httpClient"/>, so this client needs no
    /// API key or signature service of its own. Use <see cref="Create"/> to build a fully-wired client.
    /// </summary>
    /// <param name="httpClient">The (resilient) HTTP client used for all requests.</param>
    /// <param name="receiveWindow">The Binance <c>recvWindow</c> in milliseconds applied to signed requests.</param>
    /// <param name="ownsHttpClient">
    /// When <see langword="true"/>, <paramref name="httpClient"/> is disposed together with this client.
    /// Defaults to <see langword="false"/> so externally-owned clients are never disposed by this SDK.
    /// </param>
    public BinanceExchangeClient(
        HttpClient httpClient,
        decimal receiveWindow = 5000m,
        bool ownsHttpClient = false)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;

        _http = new BinanceHttpClient(httpClient, receiveWindow);
        var symbolMapper = new BinanceSymbolMapper();

        var mapperConfig = MapperConfiguration.Create(cfg =>
            cfg.AddProfile(new BinanceResponseProfile(symbolMapper)));
        mapperConfig.AssertConfigurationIsValid();
        var mapper = mapperConfig.CreateMapper();

        MarketData = new BinanceMarketDataService(_http, symbolMapper, mapper);
        Trading = new BinanceTradingService(_http, symbolMapper, mapper);
        Account = new BinanceAccountService(_http, symbolMapper, mapper);
    }

    /// <inheritdoc />
    public ExchangeId ExchangeId => ExchangeId.Binance;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>
    /// Creates a new <see cref="BinanceExchangeClient"/> using the specified options.
    /// </summary>
    public static BinanceExchangeClient Create(BinanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inner = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        BinanceSignatureService? sig = string.IsNullOrEmpty(options.SecretKey) ? null : new(options.SecretKey);

        var resilienceOptions = new CryptoExchanges.Net.Core.Resilience.ResilienceOptions
        {
            UsageHeaderName = "X-MBX-USED-WEIGHT-1m"
        };
        var translator = new Resilience.BinanceErrorTranslator();
        var gate = new CryptoExchanges.Net.Http.ReactiveRateLimitGate();
        var offsetHolder = new long[] { 0L };

        Resilience.BinanceSigningHandler? signing = sig is null
            ? null
            : new Resilience.BinanceSigningHandler(options.ApiKey, sig, () => Interlocked.Read(ref offsetHolder[0]));

        var hc = CryptoExchanges.Net.Http.HttpClientPipelineBuilder.Build(
            inner, resilienceOptions, translator, gate, requestFinalizer: signing);
        hc.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        hc.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
        // Api-key-only clients (no secret -> no signing handler) still need the key header.
        // When a secret IS present, the signing handler removes+re-adds this header, so no duplicate.
        if (!string.IsNullOrEmpty(options.ApiKey))
            hc.DefaultRequestHeaders.Add("X-MBX-APIKEY", options.ApiKey);

        var client = new BinanceExchangeClient(hc, options.ReceiveWindow, ownsHttpClient: true);
        // Share the SAME offset array instance the signing handler's closure reads, so
        // SyncServerTimeAsync writes are observed by the handler on the next signed request.
        client._offsetHolder = offsetHolder;
        return client;
    }

    /// <summary>
    /// Creates a new <see cref="BinanceExchangeClient"/> from environment variables.
    /// Reads BINANCE_API_KEY and BINANCE_SECRET_KEY from environment.
    /// </summary>
    public static BinanceExchangeClient CreateFromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? string.Empty;
        var secretKey = Environment.GetEnvironmentVariable("BINANCE_SECRET_KEY") ?? string.Empty;

        return Create(new()
        {
            ApiKey = apiKey,
            SecretKey = secretKey
        });
    }

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
        if (_ownsHttpClient)
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
