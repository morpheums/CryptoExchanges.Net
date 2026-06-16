using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

public class BinanceSymbolMapperTests
{
    private static SymbolInfo Info(string b, string q)
        => new(new Symbol(Asset.Of(b), Asset.Of(q)), new[] { OrderType.Limit });

    [Fact]
    public void ToWire_ConcatenatesTickers()
        => new BinanceSymbolMapper().ToWire(new Symbol(Asset.Btc, Asset.Usdt)).Should().Be("BTCUSDT");

    [Fact]
    public void FromComponents_BuildsTypedSymbol()
        => new BinanceSymbolMapper().FromComponents("btc", "usdt").Should().Be(new Symbol(Asset.Btc, Asset.Usdt));

    [Fact]
    public void FromWire_ExactMatch_FromUpdatedTable()
    {
        var mapper = new BinanceSymbolMapper();
        mapper.Update(new[] { Info("BTC", "USDT") });
        mapper.FromWire("BTCUSDT").Should().Be(new Symbol(Asset.Btc, Asset.Usdt));
    }

    [Fact]
    public void FromWire_ColdCache_FallsBackToKnownQuoteSuffix()
        => new BinanceSymbolMapper().FromWire("ETHUSDT").Should().Be(new Symbol(Asset.Eth, Asset.Usdt));

    [Fact]
    public void FromWire_Unresolvable_Throws()
    {
        var act = () => new BinanceSymbolMapper().FromWire("ZZZZZZ");
        act.Should().Throw<FormatException>();
    }
}
