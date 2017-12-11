using CryptoExchanges.Net.Binance.Resolver;
using CryptoExchanges.Net.Domain.Resolvers;
using Ninject.Modules;

namespace CryptoExchanges.Net.DI.Configurations.Ninject
{
    public class ResolversDIModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IExchangeClientResolver>().To<ExchangeClientResolver>();
        }
    }
}
