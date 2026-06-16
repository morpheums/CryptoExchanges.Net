using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
/// <param name="httpClient">The HTTP client used for all requests.</param>
/// <param name="apiKey">The Binance API key.</param>
/// <param name="signatureService">The signature service used for signed requests, or <see langword="null"/>.</param>
/// <param name="receiveWindow">The Binance <c>recvWindow</c> in milliseconds applied to signed requests.</param>
/// <param name="ownsHttpClient">
/// When <see langword="true"/>, <paramref name="httpClient"/> is disposed together with this client.
/// Defaults to <see langword="false"/> so externally-owned clients are never disposed by this SDK.
/// </param>
public sealed class BinanceExchangeClient(
    HttpClient httpClient,
    string apiKey,
    BinanceSignatureService? signatureService,
    decimal receiveWindow = 5000m,
    bool ownsHttpClient = false) : IExchangeClient, IAsyncDisposable
{
    /// <inheritdoc />
    public ExchangeId ExchangeId => ExchangeId.Binance;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; } =
        new BinanceMarketDataService(new(httpClient, apiKey, signatureService, receiveWindow));

    /// <inheritdoc />
    public ITradingService Trading { get; } =
        new BinanceTradingService(new(httpClient, apiKey, signatureService, receiveWindow));

    /// <inheritdoc />
    public IAccountService Account { get; } =
        new BinanceAccountService(new(httpClient, apiKey, signatureService, receiveWindow));

    /// <summary>
    /// Creates a new <see cref="BinanceExchangeClient"/> using the specified options.
    /// </summary>
    public static BinanceExchangeClient Create(BinanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };

        var hc = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        hc.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");

        BinanceSignatureService? sig = null;
        if (!string.IsNullOrEmpty(options.SecretKey))
            sig = new(options.SecretKey);

        return new(hc, options.ApiKey, sig, options.ReceiveWindow, ownsHttpClient: true);
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

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var requestUri = new Uri("/api/v3/time", UriKind.Relative);
            using var resp = await httpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var _ = await resp.Content.ReadFromJsonAsync<BinanceServerTimeResponse>(cancellationToken: ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (ownsHttpClient)
            httpClient.Dispose();
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
