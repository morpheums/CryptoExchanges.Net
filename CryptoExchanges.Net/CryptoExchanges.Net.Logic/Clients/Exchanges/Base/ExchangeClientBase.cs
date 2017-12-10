using CryptoExchanges.Net.Domain.Clients;
using CryptoExchanges.Net.Domain.Enums;

namespace CryptoExchanges.Net.Logic.Clients.Exchanges.Base
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class ExchangeClientBase
    {
        /// <summary>
        /// Client to be used to call the API.
        /// </summary>
        public readonly IApiClient _apiClient;

        /// <summary>
        /// Defines the constructor of the client.
        /// </summary>
        /// <param name="apiClient">API client to be used for API calls.</param>
        public ExchangeClientBase(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }
    }
}
