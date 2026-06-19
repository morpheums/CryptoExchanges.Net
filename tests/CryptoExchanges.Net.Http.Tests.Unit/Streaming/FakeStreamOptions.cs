using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// Minimal options type for <see cref="StreamServiceRegistration"/> DI tests.
/// Carries no real configuration — just a tag for asserting options-configure wiring.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by the DI container via options infrastructure.")]
internal sealed class FakeStreamOptions
{
    /// <summary>Test tag set by the configure delegate; asserted in DI tests.</summary>
    public string TestTag { get; set; } = string.Empty;
}
