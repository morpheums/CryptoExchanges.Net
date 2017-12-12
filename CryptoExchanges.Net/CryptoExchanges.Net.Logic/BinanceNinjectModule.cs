using CryptoExchanges.Net.Binance.Clients.API;
using CryptoExchanges.Net.Binance.CustomParser;
using CryptoExchanges.Net.Domain;
using Ninject.Modules;

namespace CryptoExchanges.Net.Binance
{
    public class BinanceNinjectModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IBinanceApiHelper>().To<BinanceApiHelper>();
            Bind<IBinanceCustomParser>().To<BinanceCustomParser>();
            Bind<IExchangeClient>().To<BinanceClient>();
        }
    }
}
