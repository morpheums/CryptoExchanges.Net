namespace CryptoExchanges.Net.Domain
{
    public interface ICryptoClient
    {
        IExchangeClient GetExchange(string exchangeKey);
    }
}
