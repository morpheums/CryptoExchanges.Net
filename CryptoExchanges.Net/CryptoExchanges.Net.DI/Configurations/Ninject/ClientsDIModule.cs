using CryptoExchanges.Net.Domain.Clients;
using CryptoExchanges.Net.Logic.Clients.Exchanges;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.DI.Configurations.Ninject
{
    public class ClientsDIModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IExchangeClient>().To<BinanceClient>();
        }
    }

}
