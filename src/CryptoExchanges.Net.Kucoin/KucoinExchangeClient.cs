using System.Diagnostics.CodeAnalysis;
using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Services;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Kucoin;

/// <summary>
/// Full KuCoin exchange client implementing <see cref="IExchangeClient"/>.
/// Composes market data, trading, and account services over the V1/V2 spot REST API.
/// </summary>
public sealed class KucoinExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;
    // CA1859 suppressed: interface type is intentional — enables typed-client DI and unit testing.
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "IKucoinHttpClient is held intentionally for DI / testability.")]
    private readonly IKucoinHttpClient _http;

    /// <summary>
    /// Clock-skew offset (in ms) shared with the signing handler. Single-element array so the handler's
    /// closure and <see cref="SyncServerTimeAsync"/> read/write the SAME instance.
    /// </summary>
    private readonly long[] _offsetHolder;
    private readonly Core.Resilience.IExchangeTimeSync _timeSync;

    /// <summary>
    /// Internal composition constructor — called exclusively by <see cref="Internal.KucoinClientComposer"/>.
    /// Signing and the KC-API-* headers are owned by the resilience pipeline on
    /// <paramref name="httpClient"/>, so this client needs no API key / secret / passphrase of its own.
    /// </summary>
    internal KucoinExchangeClient(
        IKucoinHttpClient http,
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
    public ExchangeId ExchangeId => ExchangeId.Kucoin;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>Creates a fully-wired client (container-free) for the given options.</summary>
    /// <param name="options">The KuCoin client options.</param>
    public static KucoinExchangeClient Create(KucoinOptions options)
        => Internal.KucoinClientComposer.Create(options);

    /// <summary>Creates a client from KUCOIN_API_KEY / KUCOIN_SECRET_KEY / KUCOIN_PASSPHRASE environment variables.</summary>
    public static KucoinExchangeClient CreateFromEnvironment()
        => Create(new()
        {
            ApiKey = Environment.GetEnvironmentVariable("KUCOIN_API_KEY") ?? string.Empty,
            SecretKey = Environment.GetEnvironmentVariable("KUCOIN_SECRET_KEY") ?? string.Empty,
            Passphrase = Environment.GetEnvironmentVariable("KUCOIN_PASSPHRASE") ?? string.Empty
        });

    /// <summary>
    /// Syncs the clock-skew offset from KuCoin server time so signed requests don't trip a timestamp-expiry
    /// rejection. Opt-in: call once after construction if the local clock may be skewed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SyncServerTimeAsync(CancellationToken ct = default)
    {
        // KuCoin /api/v1/timestamp returns {"code":"200000","data":<ms>} — data is a raw long,
        // so ResponseDto<long> is the correct shape. Using ResponseDto<ServerTimeDto> would cause a
        // double-wrap ({"data":{"data":...}}) and fail to deserialize.
        var resp = await _http.GetAsync<ResponseDto<long>>("/api/v1/timestamp", signed: false, ct: ct).ConfigureAwait(false);
        var serverTimeMs = resp.Data;
        if (serverTimeMs > 0)
            _timeSync.ApplyOffset(serverTimeMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            _ = await _http.GetAsync<ResponseDto<long>>("/api/v1/timestamp", signed: false, ct: ct).ConfigureAwait(false);
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
