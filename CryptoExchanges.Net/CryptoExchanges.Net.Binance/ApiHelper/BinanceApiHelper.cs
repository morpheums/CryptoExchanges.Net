using System.Threading.Tasks;
using System;
using System.Net.Http;
using Newtonsoft.Json;
using CryptoExchanges.Net.Models.Enums;
using CryptoExchanges.Net.Utils;
using Newtonsoft.Json.Linq;
using System.Net;

namespace CryptoExchanges.Net.Binance.Clients.API
{
    public class BinanceApiHelper : IBinanceApiHelper
    {
        #region Variables
        /// <summary>
        /// URL of the API.
        /// </summary>
        private string _apiUrl;

        /// <summary>
        /// Key used to authenticate within the API.
        /// </summary>
        private string _apiKey;

        /// <summary>
        /// API secret used to signed API calls.
        /// </summary>
        private string _apiSecret;

        /// <summary>
        /// HttpClient to be used to call the API.
        /// </summary>
        private HttpClient _httpClient;
        #endregion

        /// <summary>
        /// Defines the constructor of the Api Client.
        /// </summary>
        public BinanceApiHelper()
        {
        }

        #region Methods
        /// <summary>
        /// Configures the HTTPClient.
        /// </summary>
        private void ConfigureHttpClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_apiUrl)
            };

            _httpClient.DefaultRequestHeaders
                 .Add("X-MBX-APIKEY", _apiKey);

            _httpClient.DefaultRequestHeaders
                    .Accept
                    .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <see cref="IBinanceApiHelper.SetCredentials(string, string, string)"/>
        public void SetCredentials(string apiUrl, string apiKey, string apiSecret)
        {
            _apiUrl = apiUrl;
            _apiKey = apiKey;
            _apiSecret = apiSecret;

            ConfigureHttpClient();
        }

        /// <see cref="IBinanceApiHelper.Call{T}(ApiMethod, string, bool, string)"/>
        public T Call<T>(ApiMethod method, string endpoint, bool isSigned = false, string parameters = null)
        {
            var finalEndpoint = endpoint + (string.IsNullOrWhiteSpace(parameters) ? "" : $"?{parameters}");

            if (isSigned)
            {
                // Joining provided parameters
                parameters += (!string.IsNullOrWhiteSpace(parameters) ? "&timestamp=" : "timestamp=") + Utilities.GenerateTimeStamp(DateTime.Now.ToUniversalTime());

                // Creating request signature
                var signature = Utilities.GenerateSignature(_apiSecret, parameters);
                finalEndpoint = $"{endpoint}?{parameters}&signature={signature}";
            }

            var request = new HttpRequestMessage(Utilities.CreateHttpMethod(method.ToString()), finalEndpoint);
            var response = _httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                // Api return is OK
                response.EnsureSuccessStatusCode();

                // Get the result
                var result = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                // Serialize and return result
                return JsonConvert.DeserializeObject<T>(result);
            }

            // We received an error
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                throw new Exception("Api Request Timeout.");
            }

            // Get te error code and message
            var e = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            // Error Values
            var errorCode = 0;
            string errorMessage = "";
            if (e.IsValidJson())
            {
                try
                {
                    var i = JObject.Parse(e);

                    errorCode = i["code"]?.Value<int>() ?? 0;
                    errorMessage = i["msg"]?.Value<string>();
                }
                catch { }
            }

            throw new Exception(string.Format("Api Error Code: {0} Message: {1}", errorCode, errorMessage));
        }

        /// <see cref="IBinanceApiHelper.CallAsync{T}(ApiMethod, string, bool, string)"/>
        public async Task<T> CallAsync<T>(ApiMethod method, string endpoint, bool isSigned = false, string parameters = null)
        {
            var finalEndpoint = endpoint + (string.IsNullOrWhiteSpace(parameters) ? "" : $"?{parameters}");

            if (isSigned)
            {
                // Joining provided parameters
                parameters += (!string.IsNullOrWhiteSpace(parameters) ? "&timestamp=" : "timestamp=") + Utilities.GenerateTimeStamp(DateTime.Now.ToUniversalTime());

                // Creating request signature
                var signature = Utilities.GenerateSignature(_apiSecret, parameters);
                finalEndpoint = $"{endpoint}?{parameters}&signature={signature}";
            }

            var request = new HttpRequestMessage(Utilities.CreateHttpMethod(method.ToString()), finalEndpoint);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // Api return is OK
                response.EnsureSuccessStatusCode();

                // Get the result
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Serialize and return result
                return JsonConvert.DeserializeObject<T>(result);
            }

            // We received an error
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                throw new Exception("Api Request Timeout.");
            }

            // Get te error code and message
            var e = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Error Values
            var errorCode = 0;
            string errorMessage = "";
            if (e.IsValidJson())
            {
                try
                {
                    var i = JObject.Parse(e);

                    errorCode = i["code"]?.Value<int>() ?? 0;
                    errorMessage = i["msg"]?.Value<string>();
                }
                catch { }
            }

            throw new Exception(string.Format("Api Error Code: {0} Message: {1}", errorCode, errorMessage));
        }

        /// <see cref="IBinanceApiHelper.HasCredentials"/>
        public bool HasCredentials()
        {
            return !(string.IsNullOrWhiteSpace(_apiKey) && string.IsNullOrWhiteSpace(_apiSecret));
        }
        #endregion
    }
}
