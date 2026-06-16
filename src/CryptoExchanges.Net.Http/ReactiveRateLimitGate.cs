using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Http;

/// <summary>
/// Reactive per-client rate-limit gate (M1). It does not pre-throttle; it only enforces a
/// cooldown when the exchange returned a Retry-After (429/418). The proactive weight-window
/// implementation is a fast-follow that replaces this class behind the same interface.
/// Thread-safe: the release time is published via Interlocked and never held across a delay.
/// </summary>
public sealed class ReactiveRateLimitGate : IRateLimitGate
{
    private long _releaseTicksUtc;

    /// <inheritdoc />
    public ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        var release = Interlocked.Read(ref _releaseTicksUtc);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        if (release <= now)
            return ValueTask.CompletedTask;

        var delay = TimeSpan.FromTicks(release - now);
        return new ValueTask(Task.Delay(delay, cancellationToken));
    }

    /// <inheritdoc />
    public void Observe(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        if (retryAfter is null || retryAfter <= TimeSpan.Zero)
            return;

        var candidate = DateTimeOffset.UtcNow.Add(retryAfter.Value).UtcTicks;
        long current;
        do { current = Interlocked.Read(ref _releaseTicksUtc); }
        while (candidate > current &&
               Interlocked.CompareExchange(ref _releaseTicksUtc, candidate, current) != current);
    }
}
