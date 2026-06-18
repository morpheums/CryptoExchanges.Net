using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using CryptoExchanges.Net.Bitget.Services;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Bitget;

/// <summary>
/// Full Bitget exchange client implementing <see cref="IExchangeClient"/>.
/// Composes market data, trading, and account services over the V2 spot REST API.
/// </summary>
public sealed class BitgetExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;
    // CA1859 suppressed: interface type is intentional — enables typed-client DI and unit testing.
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "IBitgetHttpClient is held intentionally for DI / testability.")]
    private readonly IBitgetHttpClient _http;

    /// <summary>
    /// Clock-skew offset (in ms) shared with the signing handler. Single-element array so the handler's
    /// closure and <see cref="SyncServerTimeAsync"/> read/write the SAME instance.
    /// </summary>
    private readonly long[] _offsetHolder;
    private readonly Core.Resilience.IExchangeTimeSync _timeSync;

    /// <summary>
    /// Internal composition constructor — called exclusively by <see cref="Internal.BitgetClientComposer"/>.
    /// Signing and the four ACCESS-* headers are owned by the resilience pipeline on
    /// <paramref name="httpClient"/>, so this client needs no API key / secret / passphrase of its own.
    /// </summary>
    internal BitgetExchangeClient(
        IBitgetHttpClient http,
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
    public ExchangeId ExchangeId => ExchangeId.Bitget;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>Creates a fully-wired client (container-free) for the given options.</summary>
    /// <param name="options">The Bitget client options.</param>
    public static BitgetExchangeClient Create(BitgetOptions options)
        => Internal.BitgetClientComposer.Create(options);

    /// <summary>Creates a client from BITGET_API_KEY / BITGET_SECRET_KEY / BITGET_PASSPHRASE environment variables.</summary>
    public static BitgetExchangeClient CreateFromEnvironment()
        => Create(new()
        {
            ApiKey = Environment.GetEnvironmentVariable("BITGET_API_KEY") ?? string.Empty,
            SecretKey = Environment.GetEnvironmentVariable("BITGET_SECRET_KEY") ?? string.Empty,
            Passphrase = Environment.GetEnvironmentVariable("BITGET_PASSPHRASE") ?? string.Empty
        });

    /// <summary>
    /// Syncs the clock-skew offset from Bitget server time so signed requests don't trip a
    /// timestamp-expiry rejection. Opt-in: call once after construction if the local clock may be skewed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SyncServerTimeAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync<BitgetObjectResponse<BitgetServerTime>>("/api/v2/public/time", signed: false, ct: ct).ConfigureAwait(false);
        var serverTimeMs = ServerTimeMs(resp.Data);
        _timeSync.ApplyOffset(serverTimeMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            // The resilience pipeline throws typed exceptions on failure, so reaching here is success.
            _ = await _http.GetAsync<BitgetObjectResponse<BitgetServerTime>>("/api/v2/public/time", signed: false, ct: ct).ConfigureAwait(false);
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

    /// <summary>Resolves server time in unix milliseconds from the V2 time envelope (serverTime is ms as a string).</summary>
    private static long ServerTimeMs(BitgetServerTime? result)
    {
        if (result is null || string.IsNullOrEmpty(result.ServerTime))
            return 0L;
        return long.TryParse(result.ServerTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) ? ms : 0L;
    }
}

/// <summary>The <c>data</c> element of the Bitget V2 <c>/api/v2/public/time</c> response.</summary>
internal sealed record BitgetServerTime
{
    /// <summary>Server time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("serverTime")]
    public string ServerTime { get; init; } = "0";
}
