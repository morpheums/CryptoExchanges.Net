using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using CryptoExchanges.Net.Bybit.Services;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Bybit;

/// <summary>
/// Full Bybit exchange client implementing <see cref="IExchangeClient"/>.
/// Composes market data, trading, and account services over the V5 spot REST API.
/// </summary>
public sealed class BybitExchangeClient : IExchangeClient, IAsyncDisposable
{
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;
    // CA1859 suppressed: interface type is intentional — enables typed-client DI and unit testing.
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "IBybitHttpClient is held intentionally for DI / testability.")]
    private readonly IBybitHttpClient _http;

    /// <summary>
    /// Clock-skew offset (in ms) shared with the signing handler. Single-element array so the
    /// handler's closure and <see cref="SyncServerTimeAsync"/> read/write the SAME instance.
    /// </summary>
    private readonly long[] _offsetHolder;
    private readonly Core.Resilience.IExchangeTimeSync _timeSync;

    /// <summary>
    /// Internal composition constructor — called exclusively by <see cref="Internal.BybitClientComposer"/>.
    /// Signing, the API-key header, and the recv-window header are owned by the resilience pipeline on
    /// <paramref name="httpClient"/>, so this client needs no API key or signature service of its own.
    /// </summary>
    internal BybitExchangeClient(
        IBybitHttpClient http,
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
    public ExchangeId ExchangeId => ExchangeId.Bybit;

    /// <inheritdoc />
    public IMarketDataService MarketData { get; }

    /// <inheritdoc />
    public ITradingService Trading { get; }

    /// <inheritdoc />
    public IAccountService Account { get; }

    /// <summary>Creates a fully-wired client (container-free) for the given options.</summary>
    /// <param name="options">The Bybit client options.</param>
    public static BybitExchangeClient Create(BybitOptions options)
        => Internal.BybitClientComposer.Create(options);

    /// <summary>Creates a client from BYBIT_API_KEY / BYBIT_SECRET_KEY environment variables.</summary>
    public static BybitExchangeClient CreateFromEnvironment()
        => Create(new()
        {
            ApiKey = Environment.GetEnvironmentVariable("BYBIT_API_KEY") ?? string.Empty,
            SecretKey = Environment.GetEnvironmentVariable("BYBIT_SECRET_KEY") ?? string.Empty
        });

    /// <summary>
    /// Syncs the clock-skew offset from Bybit server time so signed requests don't trip retCode 10002.
    /// Opt-in: call once after construction if the local clock may be skewed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SyncServerTimeAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync<BybitResponse<BybitServerTimeResult>>("/v5/market/time", signed: false, ct: ct).ConfigureAwait(false);
        var serverTimeMs = ServerTimeMs(resp.Result);
        _timeSync.ApplyOffset(serverTimeMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            // The resilience pipeline throws typed exceptions on failure, so reaching here is success.
            _ = await _http.GetAsync<BybitResponse<BybitServerTimeResult>>("/v5/market/time", signed: false, ct: ct).ConfigureAwait(false);
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

    /// <summary>
    /// Resolves server time in unix milliseconds from the V5 time envelope, preferring the
    /// nanosecond field (truncated to ms) and falling back to the second field.
    /// </summary>
    private static long ServerTimeMs(BybitServerTimeResult? result)
    {
        if (result is null)
            return 0L;
        if (!string.IsNullOrEmpty(result.TimeNano)
            && long.TryParse(result.TimeNano, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nano))
            return nano / 1_000_000L;
        if (!string.IsNullOrEmpty(result.TimeSecond)
            && long.TryParse(result.TimeSecond, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sec))
            return sec * 1_000L;
        return 0L;
    }
}

/// <summary>The <c>result</c> shape of the Bybit V5 <c>/v5/market/time</c> response.</summary>
internal sealed record BybitServerTimeResult
{
    /// <summary>Server time in unix seconds (string-encoded).</summary>
    [JsonPropertyName("timeSecond")]
    public string TimeSecond { get; init; } = "0";

    /// <summary>Server time in unix nanoseconds (string-encoded).</summary>
    [JsonPropertyName("timeNano")]
    public string TimeNano { get; init; } = "0";
}
