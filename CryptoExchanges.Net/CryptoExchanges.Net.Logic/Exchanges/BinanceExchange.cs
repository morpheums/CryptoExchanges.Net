using CryptoExchanges.Net.Domain.Exchanges;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoExchanges.Net.Domain.Enums;

namespace CryptoExchanges.Net.Logic.Exchanges
{
    public class BinanceExchange : IExchange
    {
        public string Key { get { return "Binance"; } }
        public string Name { get { return "Binance"; } }
        public string Url { get { return @"https://www.binance.com"; } }
        public string ApiKey { get => throw new NotImplementedException(); }
        public string ApiSecret { get => throw new NotImplementedException(); }
    }
}
