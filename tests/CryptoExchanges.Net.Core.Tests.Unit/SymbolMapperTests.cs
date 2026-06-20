using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Tests.Unit;

public class SymbolMapperTests
{
    private static SymbolInfo Info(string b, string q)
        => new(new Symbol(Asset.Of(b), Asset.Of(q)), new[] { OrderType.Limit });

    private static readonly SymbolFormat Binance = new()
    {
        Delimiter = "", Casing = SymbolCasing.Upper,
        FallbackQuoteAssets = ["USDT", "USDC", "USD", "BTC", "ETH"]
    };
    private static readonly SymbolFormat Hyphen = new() { Delimiter = "-", Casing = SymbolCasing.Upper };
    private static readonly SymbolFormat Lower = new() { Delimiter = "", Casing = SymbolCasing.Lower };
    private static readonly SymbolFormat Kraken = new()
    {
        Delimiter = "", Casing = SymbolCasing.Upper,
        AssetAliases = new Dictionary<string, string> { ["BTC"] = "XBT", ["USD"] = "ZUSD" }
    };

    [Fact]
    public void ToWire_Concatenates_NoDelimiter()
        => new SymbolMapper(Binance).ToWire(new Symbol(Asset.Btc, Asset.Usdt)).Should().Be("BTCUSDT");

    [Fact]
    public void ToWire_Delimited()
        => new SymbolMapper(Hyphen).ToWire(new Symbol(Asset.Btc, Asset.Usdt)).Should().Be("BTC-USDT");

    [Fact]
    public void ToWire_Lowercase()
        => new SymbolMapper(Lower).ToWire(new Symbol(Asset.Btc, Asset.Usdt)).Should().Be("btcusdt");

    [Fact]
    public void ToWire_AppliesAliases()
        => new SymbolMapper(Kraken).ToWire(new Symbol(Asset.Btc, Asset.Of("USD"))).Should().Be("XBTZUSD");

    [Fact]
    public void FromComponents_BuildsSymbol()
        => new SymbolMapper(Binance).FromComponents("btc", "usdt").Should().Be(new Symbol(Asset.Btc, Asset.Usdt));

    [Fact]
    public void FromComponents_ReverseAliases()
        => new SymbolMapper(Kraken).FromComponents("XBT", "ZUSD").Should().Be(new Symbol(Asset.Btc, Asset.Of("USD")));

    [Fact]
    public void FromWire_ExactMatch_FromTable()
    {
        var m = new SymbolMapper(Binance);
        m.UpdateSymbols(new[] { Info("BTC", "USDT") });
        m.FromWire("BTCUSDT").Should().Be(new Symbol(Asset.Btc, Asset.Usdt));
    }

    [Fact]
    public void FromWire_Delimited_Splits()
        => new SymbolMapper(Hyphen).FromWire("BTC-USDT").Should().Be(new Symbol(Asset.Btc, Asset.Usdt));

    [Fact]
    public void FromWire_ColdCache_QuoteSuffixFallback()
        => new SymbolMapper(Binance).FromWire("ETHUSDT").Should().Be(new Symbol(Asset.Eth, Asset.Usdt));

    [Fact]
    public void FromWire_Unresolvable_Throws()
    {
        var act = () => new SymbolMapper(Binance).FromWire("ZZZZZZ");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void RoundTrip_AliasedSymbol_ViaWarmTable()
    {
        var m = new SymbolMapper(Kraken);
        var sym = new Symbol(Asset.Btc, Asset.Of("USD"));
        m.UpdateSymbols(new[] { Info("BTC", "USD") });   // table keyed by ToWire => "XBTZUSD"
        m.FromWire(m.ToWire(sym)).Should().Be(sym);       // "XBTZUSD" -> BTC/USD
    }
}
