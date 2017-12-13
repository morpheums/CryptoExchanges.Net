namespace CryptoExchanges.Net.Domain
{
    public interface ICryptoClient
    {
        IExchangeClient GetExchangeClient(string exchangeKey);
    }
}
