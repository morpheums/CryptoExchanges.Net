using CryptoExchanges.Net.Domain.Exchanges;
using CryptoExchanges.Net.Logic.Exchanges;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.DI.Configurations.Ninject
{
    public class ExchangesDIModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IExchange>().To<BinanceExchange>();
        }
    }
}
