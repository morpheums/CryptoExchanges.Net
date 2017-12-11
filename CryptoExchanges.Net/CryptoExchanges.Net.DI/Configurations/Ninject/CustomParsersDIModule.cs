using CryptoExchanges.Net.Binance.CustomParsers;
using Ninject.Modules;

namespace CryptoExchanges.Net.DI.Configurations.Ninject
{
    public class CustomParsersDIModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IBinanceCustomParser>().To<BinanceCustomParser>();
        }
    }
}
