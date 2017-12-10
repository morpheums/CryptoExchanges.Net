using CryptoExchanges.Net.Domain.Enums;
using CryptoExchanges.Net.Domain.Exchanges;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Domain.Resolvers
{
    public interface IExchangeResolver
    {
        IExchange GetExchange(string exchangeKey);
    }
}
