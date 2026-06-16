using System.Net;
using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Http.Tests.Unit;

public class RateLimitThrottleHandlerTests
{
    [Fact]
    public async Task CallsGate_WaitBeforeSend_AndObserveAfter()
    {
        var gate = new RecordingGate();
        var handler = new RateLimitThrottleHandler(gate)
        {
            InnerHandler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK))
        };
        using var c = new HttpClient(handler) { BaseAddress = new Uri("https://x") };
        await c.GetAsync("/p", TestContext.Current.CancellationToken);
        gate.Waited.Should().BeTrue();
        gate.Observed.Should().BeTrue();
    }

    [Fact]
    public void ReactiveGate_Wait_IsImmediate_WhenNoRetryAfterSeen()
    {
        var gate = new ReactiveRateLimitGate();
        gate.WaitAsync().IsCompleted.Should().BeTrue();
    }

    private sealed class RecordingGate : IRateLimitGate
    {
        public bool Waited; public bool Observed;
        public ValueTask WaitAsync(CancellationToken ct = default) { Waited = true; return ValueTask.CompletedTask; }
        public void Observe(HttpResponseMessage response) => Observed = true;
    }
}
