
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
            _binanceClient = _cryptoClient.GetExchangeClient("Binance");
            var apiKey = ConfigurationManager.AppSettings["BinanceApiKey"];
            var apiSecret = ConfigurationManager.AppSettings["BinanceApiSecret"];
            _binanceClient.SetCredentials(apiKey, apiSecret);
        }

        [TestMethod]
        public void TestMarketMethods()
        {
            var currenciesInfo = _binanceClient.GetExchangeCurrenciesInfo();

            var allTickersInfo = _binanceClient.GetAllTickersInfo();

            var tickerInfo = _binanceClient.GetTickerInfo("ETH", "BTC");

            var allTickersPrice = _binanceClient.GetAllTickersPrice();

            var tickerPrice = _binanceClient.GetTickerPrice("ETH", "BTC");

            var orderBook = _binanceClient.GetOrderBook("ETH", "BTC");
        }

        [TestMethod]
        public void TestMarketAsyncMethods()
        {
            var currenciesInfo = _binanceClient.GetExchangeCurrenciesInfoAsync().Result;

            var allTickersInfo = _binanceClient.GetAllTickersInfoAsync().Result;

            var tickerInfo = _binanceClient.GetTickerInfoAsync("ETH", "BTC").Result;

            var allTickersPrice = _binanceClient.GetAllTickersPriceAsync().Result;

            var tickerPrice = _binanceClient.GetTickerPriceAsync("ETH", "BTC").Result;

            var orderBook = _binanceClient.GetOrderBookAsync("ETH", "BTC").Result;
        }

        [TestMethod]
        public void TestAccountMethods()
        {
            //var a = _binanceClient.GetAccoungBalance().Result;
            //var b = _binanceClient.GetCurrentOpenOrders("ETH", "BTC").Result;
            var c = _binanceClient.GetAllOrdersAsync("ETH", "BTC").Result;

            var e = _binanceClient.GetDepositHistoryAsync("ETH").Result;
            var f = _binanceClient.GetWithdrawHistoryAsync("ETH").Result;
        }

        [TestMethod]
        public void TestAccountAsyncMethods()
        {
            //var a = _binanceClient.GetAccoungBalance().Result;
            //var b = _binanceClient.GetCurrentOpenOrders("ETH", "BTC").Result;
            var c = _binanceClient.GetAllOrdersAsync("ETH", "BTC").Result;

            var e = _binanceClient.GetDepositHistoryAsync("ETH").Result;
            var f = _binanceClient.GetWithdrawHistoryAsync("ETH").Result;
        }
    }
}

