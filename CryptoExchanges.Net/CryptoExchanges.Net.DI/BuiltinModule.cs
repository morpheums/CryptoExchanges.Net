using CryptoExchanges.Net.Domain;
using CryptoExchanges.Net.Domain.Resolvers;
using Ninject.Modules;

namespace CryptoExchanges.Net.DI
{
    public class BuiltinModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IExchangeClientResolver>().To<ExchangeClientResolver>();
            Bind<ICryptoClient>().To<CryptoClient>();
        }
    }
}
