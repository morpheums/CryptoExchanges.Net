using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CryptoExchanges.Net.Okx.Services;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Okx;

/// <summary>
/// Full OKX exchange client implementing <see cref="IExchangeClient"/>.
/// Composes market data, trading, and account services over the V5 spot REST API.
/// </summary>
public sealed class OkxExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;
    // CA1859 suppressed: interface type is intentional — enables typed-client DI and unit testing.
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "IOkxHttpClient is held intentionally for DI / testability.")]
    private readonly IOkxHttpClient _http;

    /// <summary>
    /// Clock-skew offset (in ms) shared with the signing handler. Single-element array so the handler's
    /// closure and <see cref="SyncServerTimeAsync"/> read/write the SAME instance.
    /// </summary>
    private readonly long[] _offsetHolder;
    private readonly Core.Resilience.IExchangeTimeSync _timeSync;

    /// <summary>
    /// Internal composition constructor — called exclusively by <see cref="Internal.OkxClientComposer"/>.
    /// Signing and the four OK-ACCESS-* headers are owned by the resilience pipeline on
    /// <paramref name="httpClient"/>, so this client needs no API key / secret / passphrase of its own.
    /// </summary>
    internal OkxExchangeClient(
        IOkxHttpClient http,
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
    public ExchangeId ExchangeId => ExchangeId.Okx;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>Creates a fully-wired client (container-free) for the given options.</summary>
    /// <param name="options">The OKX client options.</param>
    public static OkxExchangeClient Create(OkxOptions options)
        => Internal.OkxClientComposer.Create(options);

    /// <summary>Creates a client from OKX_API_KEY / OKX_SECRET_KEY / OKX_PASSPHRASE environment variables.</summary>
    public static OkxExchangeClient CreateFromEnvironment()
        => Create(new()
        {
            ApiKey = Environment.GetEnvironmentVariable("OKX_API_KEY") ?? string.Empty,
            SecretKey = Environment.GetEnvironmentVariable("OKX_SECRET_KEY") ?? string.Empty,
            Passphrase = Environment.GetEnvironmentVariable("OKX_PASSPHRASE") ?? string.Empty
        });

    /// <summary>
    /// Syncs the clock-skew offset from OKX server time so signed requests don't trip a timestamp-expiry
    /// rejection. Opt-in: call once after construction if the local clock may be skewed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SyncServerTimeAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync<ResponseDto<ServerTimeDto>>("/api/v5/public/time", signed: false, ct: ct).ConfigureAwait(false);
        var serverTimeMs = ServerTimeMs(resp.Data.FirstOrDefault());
        // A missing/malformed /time payload (ServerTimeMs returns 0) is a degraded but non-fatal
        // response: skip the offset update (keep the prior/local clock) rather than throw.
        if (serverTimeMs > 0)
            _timeSync.ApplyOffset(serverTimeMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            // The resilience pipeline throws typed exceptions on failure, so reaching here is success.
            _ = await _http.GetAsync<ResponseDto<ServerTimeDto>>("/api/v5/public/time", signed: false, ct: ct).ConfigureAwait(false);
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

    /// <summary>Resolves server time in unix milliseconds from the V5 time envelope (ts is ms as a string).</summary>
    private static long ServerTimeMs(ServerTimeDto? result)
    {
        if (result is null || string.IsNullOrEmpty(result.Ts))
            return 0L;
        return long.TryParse(result.Ts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) ? ms : 0L;
    }
}
