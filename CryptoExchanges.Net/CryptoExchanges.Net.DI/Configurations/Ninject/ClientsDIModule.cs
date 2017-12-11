using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Binance.Clients.API;
using CryptoExchanges.Net.Domain;
using Ninject.Modules;

namespace CryptoExchanges.Net.DI.Configurations.Ninject
{
    public class ClientsDIModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IExchangeClient>().To<BinanceClient>();
            Bind<IBinanceApiHelper>().To<BinanceApiHelper>();
        }
    }

}
