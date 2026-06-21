using System.Net;
using System.Net.Http;
using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Kucoin.Streaming;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests proving that the caller-configurable
/// <see cref="KucoinStreamOptions.RestBaseUrl"/> actually drives the host used for the
/// bullet-public negotiation (the option used to be silently ignored), and that an
/// invalid value fails fast at the consumption point.
/// </summary>
[Trait("Category", "Unit")]
public class KucoinStreamOptionsWiringTests
{
    private const string SandboxBaseUrl = "https://sandbox-api.kucoin.com";

    [Fact]
    public async Task AddKucoinStreams_CustomRestBaseUrl_IsHostUsedForBulletPublicNegotiation()
    {
        var capture = new CapturingHandler();

        var services = new ServiceCollection();
        services.AddKucoinExchange();
        services.AddKucoinStreams(o => o.RestBaseUrl = SandboxBaseUrl);
        // Replace the primary handler on the named "kucoin" client so the bullet-public POST
        // is captured instead of hitting the network (last ConfigurePrimaryHttpMessageHandler wins).
        services.AddHttpClient(StreamServiceCollectionExtensions.KucoinClientName)
            .ConfigurePrimaryHttpMessageHandler(() => capture);

        await using var sp = services.BuildServiceProvider();

        var streamClient = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Kucoin);
        var bulletClient = ExtractBulletClient(streamClient);

        // Drive the real negotiation through the production HTTP path. Negotiation succeeds
        // (canned response returns a trusted wss host), so no exception is expected here.
        await bulletClient.NegotiateAsync(CancellationToken.None);

        capture.LastRequestUri.Should().NotBeNull();
        capture.LastRequestUri!.Host.Should().Be("sandbox-api.kucoin.com");
        capture.LastRequestUri.AbsolutePath.Should().Be("/api/v1/bullet-public");
    }

    [Fact]
    public async Task AddKucoinStreams_DefaultRestBaseUrl_NegotiatesAgainstProductionHost()
    {
        var capture = new CapturingHandler();

        var services = new ServiceCollection();
        services.AddKucoinExchange();
        services.AddKucoinStreams();
        services.AddHttpClient(StreamServiceCollectionExtensions.KucoinClientName)
            .ConfigurePrimaryHttpMessageHandler(() => capture);

        await using var sp = services.BuildServiceProvider();

        var streamClient = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Kucoin);
        var bulletClient = ExtractBulletClient(streamClient);

        await bulletClient.NegotiateAsync(CancellationToken.None);

        capture.LastRequestUri.Should().NotBeNull();
        capture.LastRequestUri!.Host.Should().Be("api.kucoin.com");
    }

    [Fact]
    public async Task AddKucoinStreams_WhitespaceRestBaseUrl_FailsFast()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange();
        services.AddKucoinStreams(o => o.RestBaseUrl = "   ");
        await using var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Kucoin);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task AddKucoinStreams_RelativeRestBaseUrl_FailsFast()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange();
        services.AddKucoinStreams(o => o.RestBaseUrl = "not-an-absolute-uri");
        await using var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Kucoin);
        act.Should().Throw<ArgumentException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// White-box reach into the engine to pull out the bullet-public client the production
    /// <c>AddKucoinStreams</c> wiring constructed, so we can exercise its real HTTP path.
    /// </summary>
    private static IKucoinBulletPublicClient ExtractBulletClient(IStreamClient streamClient)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var engine = streamClient.GetType().GetField("_engine", flags)!.GetValue(streamClient)!;
        var protocol = engine.GetType().GetField("_protocol", flags)!.GetValue(engine)!;
        var bulletClient = protocol.GetType().GetField("_bulletClient", flags)!.GetValue(protocol)!;
        return (IKucoinBulletPublicClient)bulletClient;
    }

    /// <summary>
    /// Captures the outgoing request URI and returns a canned, trusted bullet-public payload
    /// so the negotiation completes without touching the network.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private const string CannedBulletResponse =
            "{\"code\":\"200000\",\"data\":{\"token\":\"test-token\",\"instanceServers\":" +
            "[{\"endpoint\":\"wss://ws-api-spot.kucoin.com/\",\"pingInterval\":18000,\"pingTimeout\":10000}]}}";

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CannedBulletResponse, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
