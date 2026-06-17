namespace CryptoExchanges.Net.Core.Resilience;

/// <summary>
/// Venue-neutral resilience knobs (plain scalars — no Polly types, so Core stays
/// dependency-light). Each exchange-client instance gets its own configured copy.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>Maximum retry attempts for transient failures on idempotent (GET) requests.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base backoff delay (exponential backoff with jitter is applied).</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Upper bound on a single backoff delay.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Per-attempt timeout (distinct from the overall HttpClient timeout).</summary>
    public TimeSpan PerAttemptTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Response header reporting current rate-limit usage (used by the proactive gate).</summary>
    public string? UsageHeaderName { get; set; }

    /// <summary>Usage fraction (0..1) above which the proactive gate begins throttling.</summary>
    public double WeightSoftCapPercent { get; set; } = 0.9;
}
