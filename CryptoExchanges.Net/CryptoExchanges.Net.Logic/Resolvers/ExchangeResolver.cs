using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoExchanges.Net.Domain.Enums;
using CryptoExchanges.Net.Domain.Exchanges;
using CryptoExchanges.Net.Domain.Resolvers;

namespace CryptoExchanges.Net.Logic.Resolver
{
    public class ExchangeResolver : IExchangeResolver
    {
        private readonly IEnumerable<IExchange> _exchanges;

        public ExchangeResolver(IEnumerable<IExchange> exchanges)
        {
            _exchanges = exchanges;
        }

        public IExchange GetExchange(string exchangeKey)
        {
            return _exchanges.FirstOrDefault(e => e.Key == exchangeKey);
        }
    }
}
