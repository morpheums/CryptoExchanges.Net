using System.Text;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Binance.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Binance.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for <see cref="BinanceStreamProtocol"/>: Classify, BuildSubscribe,
/// BuildUnsubscribe. Canned byte frames only — no sockets.
/// </summary>
public class BinanceStreamProtocolTests
{
    private static readonly BinanceStreamOptions DefaultOptions = new();

    private static BinanceStreamProtocol MakeProtocol() => new(DefaultOptions);

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    // ── Classify ─────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_CombinedStreamDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"stream\":\"btcusdt@ticker\",\"data\":{\"s\":\"BTCUSDT\"}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("btcusdt@ticker");
    }

    [Fact]
    public void Classify_SubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"result\":null,\"id\":1}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_ErrorFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"code\":-1121,\"msg\":\"Invalid symbol.\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_UnrecognisedFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"mystery\":true}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    // ── BuildSubscribe ────────────────────────────────────────────────────────

    [Fact]
    public void BuildSubscribe_Ticker_ProducesCorrectToken()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("method").GetString().Should().Be("SUBSCRIBE");
        doc.RootElement.GetProperty("params")[0].GetString().Should().Be("btcusdt@ticker");
    }

    [Fact]
    public void BuildSubscribe_OrderBookWithDepth_ProducesDepthToken()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "ETHUSDT", Depth: 20);

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("params")[0].GetString().Should().Be("ethusdt@depth20");
    }

    [Fact]
    public void BuildSubscribe_Kline_ProducesIntervalToken()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneHour));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("params")[0].GetString().Should().Be("btcusdt@kline_1h");
    }

    [Fact]
    public void BuildUnsubscribe_Trade_ProducesUnsubscribeMethod()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "BTCUSDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("method").GetString().Should().Be("UNSUBSCRIBE");
        doc.RootElement.GetProperty("params")[0].GetString().Should().Be("btcusdt@trade");
    }

    // ── HeartbeatPolicy ───────────────────────────────────────────────────────

    [Fact]
    public void Heartbeat_IsServerPingClientPong()
    {
        var protocol = MakeProtocol();
        protocol.Heartbeat.Direction.Should().Be(HeartbeatDirection.ServerPingClientPong);
    }
}
