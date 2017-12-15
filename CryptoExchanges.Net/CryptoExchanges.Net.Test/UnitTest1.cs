
using CryptoExchanges.Net.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Configuration;

namespace CryptoExchanges.Net.Test
{
    [TestClass]
    public class UnitTest1
    {
        ICryptoClient _cryptoClient = new CryptoClientFactory().CreateCryptoClient();
        IExchangeClient _binanceClient;

        public UnitTest1()
        {
            _binanceClient = _cryptoClient.GetBinanceClient();
            var apiKey = ConfigurationManager.AppSettings["BinanceApiKey"];
            var apiSecret = ConfigurationManager.AppSettings["BinanceApiSecret"];
            _binanceClient.SetCredentials(apiKey, apiSecret);
        }

        [TestMethod]
        public void Test()
        {
            //var accountInfo = _binanceClient.GetAccountInfo().Result;

            //var t = binanceClient.GetTradeList("ethbtc").Result;

            //var a = _binanceClient.GetExchangeCurrenciesInfo().Result;
            //var b = _binanceClient.GetAllTickersInfo().Result;
            //var c = _binanceClient.GetTickerInfo("ETH", "BTC").Result;

            var d = _binanceClient.GetAllTickersPrice().Result;
            var e = _binanceClient.GetTickerPrice("ETH", "BTC").Result;
            var f = _binanceClient.GetOrderBook("ETH", "BTC").Result;
        }
    }
}

