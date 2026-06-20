using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Core.Resilience;

namespace CryptoExchanges.Net.Core.Tests.Unit;

public class ExchangeTimeSyncTests
{
    private readonly ExchangeTimeSync _sut = new();

    [Fact]
    public void ComputeOffset_ReturnsServerMinusLocal()
    {
        _sut.ComputeOffset(serverTimeMs: 10_000, localNowMs: 8_000).Should().Be(2_000);
        _sut.ComputeOffset(serverTimeMs: 8_000, localNowMs: 10_000).Should().Be(-2_000);
    }

    [Fact]
    public void ApplyOffset_WritesIntoHolderAndReturnsOffset()
    {
        var holder = new long[] { 0L };
        var written = _sut.ApplyOffset(serverTimeMs: 12_345, localNowMs: 12_000, holder);

        written.Should().Be(345);
        holder[0].Should().Be(345);
    }

    [Fact]
    public void ApplyOffset_RejectsZeroLengthHolder()
    {
        var act = () => _sut.ApplyOffset(1, 0, []);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyOffset_RejectsNullHolder()
    {
        var act = () => _sut.ApplyOffset(1, 0, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyOffset_RejectsNonPositiveServerTime_LeavingHolderUntouched(long serverTimeMs)
    {
        // A 0/negative server time (missing/unparseable payload) must not write a ~-localNow offset.
        var holder = new long[] { 99L };
        var act = () => _sut.ApplyOffset(serverTimeMs, 12_000, holder);

        act.Should().Throw<ArgumentOutOfRangeException>();
        holder[0].Should().Be(99L);
    }
}
