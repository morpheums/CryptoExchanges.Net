
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
            var accountBalance = _binanceClient.GetAccountBalance();

            var currentOpenOrders = _binanceClient.GetCurrentOpenOrders("ETH", "BTC");

            var allOrders = _binanceClient.GetAllOrders("ETH", "BTC");

            var depositHistory = _binanceClient.GetDepositHistory("ETH");

            var withdrawHistory = _binanceClient.GetWithdrawHistory("ETH");
        }

        [TestMethod]
        public void TestAccountAsyncMethods()
        {
            var accountBalance = _binanceClient.GetAccountBalanceAsync().Result;

            var currentOpenOrders = _binanceClient.GetCurrentOpenOrdersAsync("ETH", "BTC").Result;

            var allOrders = _binanceClient.GetAllOrdersAsync("ETH", "BTC").Result;

            var depositHistory = _binanceClient.GetDepositHistoryAsync("ETH").Result;

            var withdrawHistory = _binanceClient.GetWithdrawHistoryAsync("ETH").Result;
        }
    }
}

