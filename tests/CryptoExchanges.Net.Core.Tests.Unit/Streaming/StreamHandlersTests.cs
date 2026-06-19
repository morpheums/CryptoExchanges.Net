using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;
using FluentAssertions;
using Xunit;

namespace CryptoExchanges.Net.Core.Tests.Unit.Streaming;

public class StreamHandlersTests
{
    [Fact]
    public void StreamHandlers_OnlyOnUpdateRequired_OptionalCallbacksDefaultToNull()
    {
        var handlers = new StreamHandlers<Ticker>(_ => ValueTask.CompletedTask);

        handlers.OnReconnecting.Should().BeNull();
        handlers.OnReconnected.Should().BeNull();
        handlers.OnLagged.Should().BeNull();
    }
}
