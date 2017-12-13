
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Configuration;

namespace CryptoExchanges.Net.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void Test()
        {
            var cryptoClient = new CryptoClientFactory().CreateCryptoClient();
            var binanceClient = cryptoClient.GetBinanceClient();

            var apiKey = ConfigurationManager.AppSettings["BinanceApiKey"];
            var apiSecret = ConfigurationManager.AppSettings["BinanceApiSecret"];

            //Set Credentials
            binanceClient.SetCredentials(apiKey, apiSecret);

            var a = binanceClient.GetAccountInfo();
        }
    }
}

