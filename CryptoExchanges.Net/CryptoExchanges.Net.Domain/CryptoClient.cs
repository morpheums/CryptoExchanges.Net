using CryptoExchanges.Net.Domain.Resolvers;

namespace CryptoExchanges.Net.Domain
{
    public class CryptoClient : ICryptoClient
    {
        IExchangeClientResolver _exchangeClientResolver;

        public CryptoClient(IExchangeClientResolver exchangeClientResolver)
        {
            _exchangeClientResolver = exchangeClientResolver;
        }

        public IExchangeClient GetExchangeClient(string exchangeKey)
        {
            return _exchangeClientResolver.GetExchangeClient(exchangeKey);
        }
    }
}
