using System.Collections.Generic;
using System.Linq;

namespace CryptoExchanges.Net.Domain.Resolvers
{
    public class ExchangeClientResolver : IExchangeClientResolver
    {
        private readonly IEnumerable<IExchangeClient> _exchangeClients;

        public ExchangeClientResolver(IEnumerable<IExchangeClient> exchangeClients)
        {
            _exchangeClients = exchangeClients;
        }

        public IExchangeClient GetExchangeClient(string exchangeKey)
        {
            return _exchangeClients.FirstOrDefault(e => e.Key.ToLower() == exchangeKey.ToLower());
        }
    }
}
