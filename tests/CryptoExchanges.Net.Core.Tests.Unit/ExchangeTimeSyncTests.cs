using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Core.Resilience;

namespace CryptoExchanges.Net.Core.Tests.Unit;

public class ExchangeTimeSyncTests
{
    [Fact]
    public void ComputeOffset_ReturnsServerMinusLocal()
    {
        ExchangeTimeSync.ComputeOffset(serverTimeMs: 10_000, localNowMs: 8_000).Should().Be(2_000);
        ExchangeTimeSync.ComputeOffset(serverTimeMs: 8_000, localNowMs: 10_000).Should().Be(-2_000);
    }

    [Fact]
    public void ApplyOffset_WritesIntoHolderAndReturnsOffset()
    {
        var holder = new long[] { 0L };
        var written = ExchangeTimeSync.ApplyOffset(serverTimeMs: 12_345, localNowMs: 12_000, holder);

        written.Should().Be(345);
        holder[0].Should().Be(345);
    }

    [Fact]
    public void ApplyOffset_RejectsZeroLengthHolder()
    {
        var act = () => ExchangeTimeSync.ApplyOffset(1, 0, []);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyOffset_RejectsNullHolder()
    {
        var act = () => ExchangeTimeSync.ApplyOffset(1, 0, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
