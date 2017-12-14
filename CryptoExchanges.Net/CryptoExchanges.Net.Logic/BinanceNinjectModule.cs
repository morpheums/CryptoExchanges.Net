using CryptoExchanges.Net.Binance.Clients.API;
using CryptoExchanges.Net.Binance.Configurations;
using CryptoExchanges.Net.Domain;
using Ninject.Modules;

namespace CryptoExchanges.Net.Binance
{
    public class BinanceNinjectModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IBinanceApiHelper>().To<BinanceApiHelper>();
            Bind<IExchangeClient>().To<BinanceClient>();
            LoadMappings();
        }

        private void LoadMappings() {
            MappingConfig.Initialize();
        }
    }
}
