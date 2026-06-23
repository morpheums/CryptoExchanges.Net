using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
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

    // ── BuildSubscribeBatch / BuildUnsubscribeBatch (TASK-072) ─────────────────

    [Fact]
    public void BuildSubscribeBatch_MultipleRequests_EmitsOneFrameWithAllParams()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 20),
            new StreamRequest(StreamKind.OrderBook, "ETHUSDT", Depth: 20),
            new StreamRequest(StreamKind.Ticker, "BNBUSDT"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("method").GetString().Should().Be("SUBSCRIBE");
        var paramsArr = doc.RootElement.GetProperty("params");
        paramsArr.GetArrayLength().Should().Be(3);
        paramsArr[0].GetString().Should().Be("btcusdt@depth20");
        paramsArr[1].GetString().Should().Be("ethusdt@depth20");
        paramsArr[2].GetString().Should().Be("bnbusdt@ticker");
        doc.RootElement.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildUnsubscribeBatch_MultipleRequests_EmitsOneUnsubscribeFrame()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTCUSDT"),
            new StreamRequest(StreamKind.Ticker, "ETHUSDT"),
        };

        var wire = protocol.BuildUnsubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("method").GetString().Should().Be("UNSUBSCRIBE");
        var paramsArr = doc.RootElement.GetProperty("params");
        paramsArr.GetArrayLength().Should().Be(2);
        paramsArr[0].GetString().Should().Be("btcusdt@ticker");
        paramsArr[1].GetString().Should().Be("ethusdt@ticker");
    }

    [Fact]
    public void BuildSubscribeBatch_SingleRequest_EmitsSingleParamArray()
    {
        var protocol = MakeProtocol();
        var requests = new[] { new StreamRequest(StreamKind.Trade, "BTCUSDT") };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        var paramsArr = doc.RootElement.GetProperty("params");
        paramsArr.GetArrayLength().Should().Be(1);
        paramsArr[0].GetString().Should().Be("btcusdt@trade");
    }

    [Fact]
    public void BuildSubscribeBatch_OneHundredRequests_EmitsExactlyOneHundredParams()
    {
        var protocol = MakeProtocol();
        var requests = Enumerable.Range(0, 100)
            .Select(i => new StreamRequest(StreamKind.Ticker, $"SYM{i}USDT"))
            .ToArray();

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("params").GetArrayLength().Should().Be(100,
            "the engine pre-chunks at 100, so one frame may carry exactly 100 tokens.");
    }

    [Fact]
    public void BuildSubscribeBatch_EmptyList_ReturnsNull()
    {
        var protocol = MakeProtocol();
        protocol.BuildSubscribeBatch([]).Should().BeNull(
            "an empty request list has no batch frame to build.");
    }

    // ── RoutingKeyFor ─────────────────────────────────────────────────────────
    // Regression for Finding 1 (subscribe/classify keyspace mismatch).
    // RoutingKeyFor (used by the engine at subscribe time) must equal Classify(frame).RoutingKey
    // (used by the engine pump at receive time). Both sides must share one venue-native keyspace.
    // These tests FAIL against old code where engine used BuildRoutingKey (canonical uppercase
    // e.g. "BTCUSDT@TICKER") while Classify returned the venue lowercase token "btcusdt@ticker".

    [Fact]
    public void RoutingKeyFor_Ticker_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
        var frame = Utf8("{\"stream\":\"btcusdt@ticker\",\"data\":{\"s\":\"BTCUSDT\"}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("btcusdt@ticker");
        classifiedKey.Should().Be("btcusdt@ticker");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Trade_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "BTCUSDT");
        var frame = Utf8("{\"stream\":\"btcusdt@trade\",\"data\":{\"e\":\"trade\"}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("btcusdt@trade");
        classifiedKey.Should().Be("btcusdt@trade");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_OrderBook_WithDepth_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 20);
        var frame = Utf8("{\"stream\":\"btcusdt@depth20\",\"data\":{\"e\":\"depthUpdate\"}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("btcusdt@depth20");
        classifiedKey.Should().Be("btcusdt@depth20");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Kline_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneMinute));
        var frame = Utf8("{\"stream\":\"btcusdt@kline_1m\",\"data\":{\"e\":\"kline\"}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("btcusdt@kline_1m");
        classifiedKey.Should().Be("btcusdt@kline_1m");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    // ── ResolveConnectionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsServerPingClientPong()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);
        info.Heartbeat.Direction.Should().Be(HeartbeatDirection.ServerPingClientPong);
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsCachedInstance()
    {
        var protocol = MakeProtocol();
        var info1 = await protocol.ResolveConnectionAsync(CancellationToken.None);
        var info2 = await protocol.ResolveConnectionAsync(CancellationToken.None);
        // Binance uses a static URL + static policy — same reference on every call.
        info1.Should().BeSameAs(info2);
    }
}
