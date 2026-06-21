using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Dtos.Streaming;

namespace CryptoExchanges.Net.Kucoin.Streaming;

/// <summary>
/// Abstraction for the bullet-public negotiation call so tests can fake the HTTP round-trip.
/// </summary>
internal interface IKucoinBulletPublicClient
{
    /// <summary>
    /// Calls <c>POST /api/v1/bullet-public</c> (unauthenticated) and returns the
    /// <see cref="BulletPublicDto"/> payload.
    /// </summary>
    /// <param name="ct">Cancellation token to abort the HTTP call.</param>
    /// <returns>The negotiation response from KuCoin.</returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct"/> is cancelled.</exception>
    Task<BulletPublicDto> NegotiateAsync(CancellationToken ct);
}

/// <summary>
/// Posts to <c>/api/v1/bullet-public</c> via the resilient HTTP client to obtain a short-lived
/// connection token and the WebSocket server list. Unauthenticated — no signing required.
/// </summary>
internal sealed class KucoinBulletPublicClient : IKucoinBulletPublicClient
{
    private readonly IKucoinHttpClient _http;

    /// <summary>
    /// Initialises the bullet-public client with the injected resilient HTTP client.
    /// </summary>
    /// <param name="http">The KuCoin resilient HTTP client.</param>
    public KucoinBulletPublicClient(IKucoinHttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <inheritdoc />
    public async Task<BulletPublicDto> NegotiateAsync(CancellationToken ct)
    {
        var response = await _http
            .PostAsync<ResponseDto<BulletPublicDto>>("/api/v1/bullet-public", parameters: null, signed: false, ct: ct)
            .ConfigureAwait(false);

        if (response?.Data is null)
            throw new InvalidOperationException("bullet-public response returned no data.");

        return response.Data;
    }
}
