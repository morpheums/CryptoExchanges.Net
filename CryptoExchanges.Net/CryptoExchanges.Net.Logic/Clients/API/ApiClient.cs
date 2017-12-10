using System.Threading.Tasks;
using CryptoExchanges.Net.Domain.Clients;
using CryptoExchanges.Net.Domain.Enums;
using CryptoExchanges.Net.Domain.Exchanges;
using CryptoExchanges.Net.Logic.Utils;
using System;
using CryptoExchanges.Net.Logic.Clients.API.Base;
using System.Net.Http;
using Newtonsoft.Json;

namespace CryptoExchanges.Net.Logic.Clients.API
{
    public class ApiClient : ApiClientBase, IApiClient
    {
        public ApiClient(IExchange exchange) : base(exchange)
        {
        }

        public async Task<T> CallAsync<T>(ApiMethod method, string endpoint, bool isSigned = false, string parameters = null)
        {
            var finalEndpoint = endpoint + (string.IsNullOrWhiteSpace(parameters) ? "" : $"?{parameters}");

            if (isSigned)
            {
                parameters += (!string.IsNullOrWhiteSpace(parameters) ? "&timestamp=" : "timestamp=") + Utilities.GenerateTimeStamp(DateTime.Now);
                var signature = Utilities.GenerateSignature(_exchange.ApiSecret, parameters);
                finalEndpoint = $"{endpoint}?{parameters}&signature={signature}";
            }

            var request = new HttpRequestMessage(Utilities.CreateHttpMethod(method.ToString()), finalEndpoint);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(result);
        }
    }
}
