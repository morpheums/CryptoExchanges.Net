using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Binance.Resilience;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

public class BinanceTimeSyncTests
{
    [Fact]
    public void ComputeOffset_ReturnsServerMinusLocal()
    {
        long offset = BinanceTimeSync.ComputeOffset(serverTimeMs: 10_000, localNowMs: 8_000);
        offset.Should().Be(2_000);
    }
}
