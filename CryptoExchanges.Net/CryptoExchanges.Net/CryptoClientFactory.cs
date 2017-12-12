using CryptoExchanges.Net.DI;
using CryptoExchanges.Net.Domain;
using Ninject;

namespace CryptoExchanges.Net
{
    public class CryptoClientFactory
    {
        public ICryptoClient CreateCryptoClient()
        {
            return Kernel.Instance.NinjectKernel.Get<ICryptoClient>();
        }
    }
}
