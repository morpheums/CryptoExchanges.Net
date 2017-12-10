using CryptoExchanges.Net.Domain.CustomParsers;
using CryptoExchanges.Net.Logic.CustomParsers;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
