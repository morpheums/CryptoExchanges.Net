using System.Collections.Generic;
using System.Linq;
using CryptoExchanges.Net.Domain.Resolvers;
using CryptoExchanges.Net.Domain;

namespace CryptoExchanges.Net.Binance.Resolver
{
    public class ExchangeClientResolver : IExchangeClientResolver
    {
        private readonly IEnumerable<IExchangeClient> _exchangeClient;

        public ExchangeClientResolver(IEnumerable<IExchangeClient> exchanges)
        {
            _exchangeClient = exchanges;
        }

        public IExchangeClient GetExchangeClient(string exchangeKey)
        {
            return _exchangeClient.FirstOrDefault(e => e.Key == exchangeKey);
        }
    }
}
