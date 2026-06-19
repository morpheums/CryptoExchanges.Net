using System.Security.Cryptography;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Engine-owned bounded exponential-backoff schedule for the reconnect loop.
/// Each call to <see cref="Next"/> returns the delay for the current attempt and
/// advances the internal state. Call <see cref="Reset"/> after a successful connection.
/// </summary>
/// <remarks>
/// Binding constraint K3: socket reconnect uses this backoff schedule, NOT the REST
/// Polly resilience pipeline. Retry stays REST-GET-only; <see cref="BackoffSchedule"/>
/// is an entirely separate code path.
/// </remarks>
internal sealed class BackoffSchedule
{
    private readonly TimeSpan _initial;
    private readonly TimeSpan _max;
    private readonly double _multiplier;

    private TimeSpan _current;
    private int _attempt;

    /// <summary>
    /// Initialises a new <see cref="BackoffSchedule"/> with the given bounds.
    /// </summary>
    /// <param name="initial">The delay used on the first reconnect attempt.</param>
    /// <param name="max">The maximum delay cap; subsequent delays are clamped to this value.</param>
    /// <param name="multiplier">
    /// The factor by which the delay grows on each attempt (e.g., 2.0 = exponential doubling).
    /// Must be ≥ 1.0.
    /// </param>
    public BackoffSchedule(TimeSpan initial, TimeSpan max, double multiplier)
    {
        if (initial <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initial), "Initial backoff must be positive.");
        if (max < initial)
            throw new ArgumentOutOfRangeException(nameof(max), "Max backoff must be >= initial.");
        if (multiplier < 1.0)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be >= 1.0.");

        _initial = initial;
        _max = max;
        _multiplier = multiplier;
        _current = initial;
    }

    /// <summary>The number of reconnect attempts made since the last <see cref="Reset"/>.</summary>
    public int Attempt => _attempt;

    /// <summary>
    /// Returns the delay for the current reconnect attempt and advances the schedule
    /// to the next (higher) delay. A small random jitter (±10 %) is applied to spread
    /// reconnect storms when multiple clients reconnect simultaneously.
    /// </summary>
    public TimeSpan Next()
    {
        _attempt++;
        var delay = _current;

        // Jitter: ± 10 % of current delay (using cryptographically random range for CA5394)
        var jitterRangeMs = (int)(delay.TotalMilliseconds * 0.1);
        var jitter = jitterRangeMs > 0
            ? RandomNumberGenerator.GetInt32(-jitterRangeMs, jitterRangeMs)
            : 0;
        var jittered = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
        if (jittered <= TimeSpan.Zero) jittered = delay;

        // Advance for the next call, capped at _max
        var next = TimeSpan.FromMilliseconds(_current.TotalMilliseconds * _multiplier);
        _current = next > _max ? _max : next;

        return jittered;
    }

    /// <summary>
    /// Resets the schedule to the initial delay after a successful connection.
    /// </summary>
    public void Reset()
    {
        _current = _initial;
        _attempt = 0;
    }
}
