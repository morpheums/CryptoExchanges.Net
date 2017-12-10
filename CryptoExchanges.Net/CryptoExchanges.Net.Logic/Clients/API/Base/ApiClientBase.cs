using CryptoExchanges.Net.Domain.Exchanges;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Logic.Clients.API.Base
{
    public class ApiClientBase
    {
        /// <summary>
        /// Client to be used to call the API.
        /// </summary>
        public readonly IExchange _exchange;

        /// <summary>
        /// HttpClient to be used to call the API.
        /// </summary>
        public readonly HttpClient _httpClient;

        /// <summary>
        /// Defines the constructor of the Api Client.
        /// </summary>
        /// <param name="apiKey">Key used to authenticate within the API.</param>
        /// <param name="apiSecret">API secret used to signed API calls.</param>
        /// <param name="apiUrl">API based url.</param>
        public ApiClientBase(IExchange exchange, bool addDefaultHeaders = true)
        {
            _exchange = exchange;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_exchange.Url)
            };

            if (addDefaultHeaders)
            {
                ConfigureHttpClient();
            }
        }

        /// <summary>
        /// Configures the HTTPClient.
        /// </summary>
        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders
                 .Add("X-MBX-APIKEY", _exchange.ApiKey);

            _httpClient.DefaultRequestHeaders
                    .Accept
                    .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
