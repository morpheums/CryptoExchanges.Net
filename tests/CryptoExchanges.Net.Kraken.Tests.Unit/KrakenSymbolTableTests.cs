using System.Net;
using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Kraken;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Kraken.Mapping;
using CryptoExchanges.Net.Kraken.Services;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kraken.Tests.Unit;

/// <summary>
/// No-network unit tests for the Kraken warm AssetPairs→wsname table (ADR-010-006).
/// All HTTP is intercepted by a fake <see cref="HttpMessageHandler"/>.
/// </summary>
public class KrakenSymbolTableTests
{
    private static readonly Symbol BtcUsd = new(Asset.Btc, Asset.Of("USD"));

    private static SymbolMapper BuildSymbolMapper()
        => new(KrakenSymbolFormat.Instance);

    private static IMapper BuildDeltaMapper(ISymbolMapper symbolMapper)
    {
        var config = MapperConfiguration.Create(cfg => cfg.AddProfile(new KrakenResponseProfile(symbolMapper)));
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    private static (KrakenHttpClient http, KrakenMarketDataService market) BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com") };
        var symbolMapper = BuildSymbolMapper();
        var http = new KrakenHttpClient(httpClient, symbolMapper);
        var mapper = BuildDeltaMapper(symbolMapper);
        var market = new KrakenMarketDataService(http, symbolMapper, mapper);
        return (http, market);
    }

    [Fact]
    public async Task SymbolTableWarmup_LegacyCode_ResolvesViaWsname()
    {
        const string assetPairsJson = """
            {
              "error": [],
              "result": {
                "XXBTZUSD": {
                  "wsname": "XBT/USD",
                  "base": "XXBT",
                  "quote": "ZUSD",
                  "ordermin": "0.0001",
                  "pair_decimals": 1,
                  "lot_decimals": 8
                }
              }
            }
            """;

        var (http, market) = BuildService(new FakeHttpHandler(assetPairsJson));

        await market.GetExchangeInfoAsync(TestContext.Current.CancellationToken);

        // After warmup, wsname "XBT/USD" reverse-maps to legacy pair code "XXBTZUSD" for REST params.
        http.ToWire(BtcUsd).Should().Be("XXBTZUSD");
    }

    [Fact]
    public void SymbolTableWarmup_BeforeWarmup_FallsBackToFormat()
    {
        var (http, _) = BuildService(new FakeHttpHandler("{}"));

        // Without warmup, KrakenSymbolFormat converts BTC→XBT and uses slash delimiter.
        var act = () => http.ToWire(BtcUsd);
        act.Should().NotThrow();
        http.ToWire(BtcUsd).Should().Be("XBT/USD");
    }

    private sealed class FakeHttpHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StringContent(responseJson, Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
