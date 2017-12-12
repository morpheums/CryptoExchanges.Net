using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CryptoExchanges.Net.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void Test()
        {
            var f = new CryptoClientFactory();
            
            var cryptoClient = f.CreateCryptoClient();

            var a = cryptoClient.GetExchange("Binance");
        }

    }
}

