namespace CryptoExchanges.Net.Domain.Resolvers
{
    public interface IExchangeClientResolver
    {
        IExchangeClient GetExchangeClient(string exchangeKey);
    }
}
