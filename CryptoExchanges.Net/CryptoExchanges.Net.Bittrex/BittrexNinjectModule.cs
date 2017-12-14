using CryptoExchanges.Net.Bittrex.Clients.API;
using CryptoExchanges.Net.Bittrex.CustomParser;
using CryptoExchanges.Net.Domain;
using Ninject.Modules;

namespace CryptoExchanges.Net.Bittrex
{
    public class BittrexNinjectModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IBittrexApiHelper>().To<BittrexApiHelper>();
            Bind<IBittrexCustomParser>().To<BittrexCustomParser>();
            Bind<IExchangeClient>().To<BittrexClient>();
        }
    }
}
