using CryptoExchanges.Net.Domain.Enums;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Bittrex.Clients.API
{
    public interface IBittrexApiHelper
    {
        /// <summary>
        /// Calls API Methods.
        /// </summary>
        /// <typeparam name="T">Type to which the response content will be converted.</typeparam>
        /// <param name="method">HTTPMethod (POST-GET-PUT-DELETE)</param>
        /// <param name="endpoint">Url endpoint.</param>
        /// <param name="isSigned">Specifies if the request needs a signature.</param>
        /// <param name="parameters">Request parameters.</param>
        /// <returns></returns>
        Task<T> CallAsync<T>(ApiMethod method, string endpoint, bool isSigned = false, string parameters = null);

        /// <summary>
        /// Method used to set the configuration for the exchange.
        /// </summary>
        /// <param name="apiKey">Key used to authenticate within the API.</param>
        /// <param name="apiSecret">API secret used to signed API calls.</param>
        /// <param name="apiUrl">API based url.</param>
        void SetCredentials(string apiUrl, string apiKey, string apiSecret);

        /// <summary>
        /// States if the credentials (Key and Secret) were provided.
        /// </summary>
        bool HasCredentials();
    }
}
