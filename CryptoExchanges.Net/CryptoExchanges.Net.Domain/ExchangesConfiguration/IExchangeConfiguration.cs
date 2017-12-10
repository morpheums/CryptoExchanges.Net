using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Domain.Exchanges
{
    public interface IExchangeConfiguration
    {
        IEnumerable<IExchange> Exchanges { get; }
    }
}
