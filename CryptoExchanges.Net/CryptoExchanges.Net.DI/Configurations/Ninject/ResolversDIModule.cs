using CryptoExchanges.Net.Domain.Resolvers;
using CryptoExchanges.Net.Logic.Resolver;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.DI.Configurations.Ninject
{
    public class ResolversDIModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IExchangeResolver>().To<ExchangeResolver>();
        }
    }
}
