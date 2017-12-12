using System.Threading.Tasks;
using CryptoExchanges.Net.Domain.Enums;
using System;
using System.Net.Http;
using Newtonsoft.Json;
using CryptoExchanges.Net.Logic.Utils;

namespace CryptoExchanges.Net.Binance.Clients.API
{
    public class BinanceApiHelper : IBinanceApiHelper
    {
        #region Variables
        /// <summary>
        /// URL of the API.
        /// </summary>
        public readonly string _apiUrl;

        /// <summary>
        /// Key used to authenticate within the API.
        /// </summary>
        public readonly string _apiKey;

        /// <summary>
        /// API secret used to signed API calls.
        /// </summary>
        public readonly string _apiSecret;

        /// <summary>
        /// HttpClient to be used to call the API.
        /// </summary>
        public readonly HttpClient _httpClient;
        #endregion

        /// <summary>
        /// Defines the constructor of the Api Client.
        /// </summary>
        /// <param name="apiKey">Key used to authenticate within the API.</param>
        /// <param name="apiSecret">API secret used to signed API calls.</param>
        /// <param name="apiUrl">API based url.</param>
        public BinanceApiHelper(string apiKey, string apiSecret, string apiUrl, bool addDefaultHeaders = true)
        {
            _apiUrl = apiUrl;
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_apiUrl)
            };

            if (addDefaultHeaders)
            {
                ConfigureHttpClient();
            }
        }

        #region Methods
        /// <summary>
        /// Configures the HTTPClient.
        /// </summary>
        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders
                 .Add("X-MBX-APIKEY", _apiKey);

            _httpClient.DefaultRequestHeaders
                    .Accept
                    .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Calls API Methods.
        /// </summary>
        /// <typeparam name="T">Type to which the response content will be converted.</typeparam>
        /// <param name="method">HTTPMethod (POST-GET-PUT-DELETE)</param>
        /// <param name="endpoint">Url endpoing.</param>
        /// <param name="isSigned">Specifies if the request needs a signature.</param>
        /// <param name="parameters">Request parameters.</param>
        /// <returns></returns>
        public async Task<T> CallAsync<T>(ApiMethod method, string endpoint, bool isSigned = false, string parameters = null)
        {
            var finalEndpoint = endpoint + (string.IsNullOrWhiteSpace(parameters) ? "" : $"?{parameters}");

            if (isSigned)
            {
                parameters += (!string.IsNullOrWhiteSpace(parameters) ? "&timestamp=" : "timestamp=") + Utilities.GenerateTimeStamp(DateTime.Now);
                var signature = Utilities.GenerateSignature(_apiSecret, parameters);
                finalEndpoint = $"{endpoint}?{parameters}&signature={signature}";
            }

            var request = new HttpRequestMessage(Utilities.CreateHttpMethod(method.ToString()), finalEndpoint);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(result);
        }
        #endregion
    }
}
