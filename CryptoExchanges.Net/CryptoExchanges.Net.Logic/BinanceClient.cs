using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoExchanges.Net.Enums;
using CryptoExchanges.Net.Models.Account;
using CryptoExchanges.Net.Models.Market;
using CryptoExchanges.Net.Domain;
using CryptoExchanges.Net.Binance.Clients.API;
using CryptoExchanges.Net.Binance.Constants;
using CryptoExchanges.Net.Binance.Utils;
using Newtonsoft.Json.Linq;
using AutoMapper;
using CryptoExchanges.Net.Models.Enums;
using CryptoExchanges.Net.Models.Common;
using CryptoExchanges.Net.Models.Params;

namespace CryptoExchanges.Net.Binance
{
    public class BinanceClient : IExchangeClient
    {
        #region Variables
        /// <summary>
        /// Client to be used to call the API.
        /// </summary>
        public readonly IBinanceApiHelper _apiClient;
        /// <summary>
        /// 
        /// </summary>
        #endregion

        #region Properties
        /// <summary>
        /// Represents the key that identifies the Exchange.
        /// </summary>
        public string Key => "Binance";

        /// <summary>
        /// Represents the Name of the Exchange.
        /// </summary>
        public string Name => "Binance Exchange";

        /// <summary>
        /// Represents the URL of the API.
        /// </summary>
        public string Url => "https://www.binance.com";

        /// <summary>
        /// Specifies the implemented API version.
        /// </summary>
        public string ApiVersion => "v3";

        public List<OrderType> SupportedOrderTypes => new List<OrderType>() { OrderType.LIMIT, OrderType.MARKET, OrderType.STOP_LOSS, OrderType.STOP_LOSS_LIMIT, OrderType.TAKE_PROFIT, OrderType.TAKE_PROFIT_LIMIT };
        #endregion

        /// <summary>
        /// ctor.
        /// </summary>
        /// <param name="apiClient">API client to be used for API calls.</param>
        public BinanceClient(IBinanceApiHelper apiClient)
        {
            _apiClient = apiClient;
        }

        #region Methods
        /// <summary>
        /// Sets the credentials (Key & Secret) to be used for calls to the exchange API.
        /// </summary>
        /// <param name="apiKey">Key to be used to authenticate within the exchange.</param>
        /// <param name="apiSecret">Secret to be used to authenticate within the exchange.</param>
        public void SetCredentials(string apiKey, string apiSecret)
        {
            _apiClient.SetCredentials(Url, apiKey, apiSecret);
        }

        /// <summary>
        /// States if the credentials (Key and Secret) were provided.
        /// </summary>
        public bool HasCredentials()
        {
            return _apiClient.HasCredentials();
        }

        public void ValidateNewOrder(NewOrderParams newOrderParams)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Market Data
        public async Task<IEnumerable<CurrencyInfo>> GetExchangeCurrenciesInfo()
        {
            var result = await _apiClient.CallAsync<JObject>(ApiMethod.GET, Endpoints.ExchangeCurrencies, false);

            var symbolsInfo = (JArray)result.GetValue("symbols");

            return Mapper.Map<IEnumerable<CurrencyInfo>>(symbolsInfo);
        }

        public async Task<IEnumerable<TickerInfo>> GetAllTickersInfo()
        {
            var result = await _apiClient.CallAsync<JArray>(ApiMethod.GET, Endpoints.TickersInfo, false);

            return Mapper.Map<IEnumerable<TickerInfo>>(result);
        }

        public async Task<TickerInfo> GetTickerInfo(string quoteSymbol, string baseSymbol)
        {
            if (string.IsNullOrWhiteSpace(quoteSymbol))
            {
                throw new ArgumentException("QuoteSymbol cannot be empty. ", "quoteSymbol");
            }

            if (string.IsNullOrWhiteSpace(baseSymbol))
            {
                throw new ArgumentException("BaseSymbol cannot be empty. ", "baseSymbol");
            }

            var pair = quoteSymbol + baseSymbol;

            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.TickersInfo, false, $"symbol={pair.ToUpper()}");

            return Mapper.Map<TickerInfo>(result);
        }

        public async Task<IEnumerable<TickerPrice>> GetAllTickersPrice()
        {
            var result = await _apiClient.CallAsync<JArray>(ApiMethod.GET, Endpoints.TickerPrice, false);

            return Mapper.Map<IEnumerable<TickerPrice>>(result);
        }

        public async Task<TickerPrice> GetTickerPrice(string quoteSymbol, string baseSymbol)
        {
            if (string.IsNullOrWhiteSpace(quoteSymbol))
            {
                throw new ArgumentException("QuoteSymbol cannot be empty. ", "quoteSymbol");
            }

            if (string.IsNullOrWhiteSpace(baseSymbol))
            {
                throw new ArgumentException("BaseSymbol cannot be empty. ", "baseSymbol");
            }

            var pair = quoteSymbol + baseSymbol;

            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.TickerPrice, false, $"symbol={pair.ToUpper()}");

            return Mapper.Map<TickerPrice>(result);
        }

        public async Task<OrderBook> GetOrderBook(string quoteSymbol, string baseSymbol)
        {
            if (string.IsNullOrWhiteSpace(quoteSymbol))
            {
                throw new ArgumentException("QuoteSymbol cannot be empty. ", "quoteSymbol");
            }

            if (string.IsNullOrWhiteSpace(baseSymbol))
            {
                throw new ArgumentException("BaseSymbol cannot be empty. ", "baseSymbol");
            }

            var pair = quoteSymbol + baseSymbol;

            var result = await _apiClient.CallAsync<JObject>(ApiMethod.GET, Endpoints.OrderBook, false, $"symbol={pair.ToUpper()}&limit=20");

            return Mapper.Map<OrderBook>(result);
        }
        #endregion

        #region Account Information

        public async Task<RequestResponse> PostNewOrder(NewOrderParams newOrderParams)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<AssetBalance>> GetAccoungBalance()
        {
            var result = await _apiClient.CallAsync<JObject>(ApiMethod.GET, Endpoints.AccountInformation, true);

            var balances = (JArray)result.GetValue("balances");

            return Mapper.Map<IEnumerable<AssetBalance>>(balances);
        }

        public async Task<Order> GetOrder(string orderId, string quoteSymbol, string baseSymbol)
        {
            var pair = quoteSymbol + baseSymbol;

            if (string.IsNullOrWhiteSpace(quoteSymbol))
            {
                throw new ArgumentException("QuoteSymbol cannot be empty. ", "quoteSymbol");
            }

            if (string.IsNullOrWhiteSpace(baseSymbol))
            {
                throw new ArgumentException("BaseSymbol cannot be empty. ", "baseSymbol");
            }

            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new ArgumentException("OrderId cannot be empty. ", "orderId");
            }

            if (!orderId.IsValidInt())
            {
                throw new ArgumentException("OrderId must be a valid integer. ", "orderId");
            }

            var args = $"symbol={pair.ToUpper()}&orderId={orderId}";

            var result = await _apiClient.CallAsync<JObject>(ApiMethod.GET, Endpoints.QueryOrder, true, args);

            return Mapper.Map<Order>(result);
        }

        public async Task<RequestResponse> CancelOrder(string orderId, string quoteSymbol = null, string baseSymbol = null)
        {
            var pair = quoteSymbol + baseSymbol;

            if (string.IsNullOrWhiteSpace(quoteSymbol))
            {
                throw new ArgumentException("QuoteSymbol cannot be empty. ", "quoteSymbol");
            }

            if (string.IsNullOrWhiteSpace(baseSymbol))
            {
                throw new ArgumentException("BaseSymbol cannot be empty. ", "baseSymbol");
            }

            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new ArgumentException("OrderId cannot be empty. ", "orderId");
            }

            if (!orderId.IsValidInt())
            {
                throw new ArgumentException("OrderId must be a valid integer. ", "orderId");
            }

            var args = $"symbol={pair.ToUpper()}&orderId={orderId}";

            var result = await _apiClient.CallAsync<JObject>(ApiMethod.GET, Endpoints.QueryOrder, true, args);

            return new RequestResponse();
        }

        public async Task<IEnumerable<Order>> GetCurrentOpenOrders(string quoteSymbol, string baseSymbol)
        {
            var pair = quoteSymbol + baseSymbol;

            if (string.IsNullOrWhiteSpace(quoteSymbol))
            {
                throw new ArgumentException("QuoteSymbol cannot be empty. ", "quoteSymbol");
            }

            if (string.IsNullOrWhiteSpace(baseSymbol))
            {
                throw new ArgumentException("BaseSymbol cannot be empty. ", "baseSymbol");
            }

            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.CurrentOpenOrders, true, $"symbol={pair.ToUpper()}");

            return Mapper.Map<IEnumerable<Order>>(result);
        }

        public async Task<IEnumerable<Order>> GetAllOrders(string quoteSymbol, string baseSymbol)
        {
            var pair = quoteSymbol + baseSymbol;

            if (string.IsNullOrWhiteSpace(quoteSymbol))
            {
                throw new ArgumentException("QuoteSymbol cannot be empty. ", "quoteSymbol");
            }

            if (string.IsNullOrWhiteSpace(baseSymbol))
            {
                throw new ArgumentException("BaseSymbol cannot be empty. ", "baseSymbol");
            }

            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.AllOrders, true, $"symbol={pair.ToUpper()}");

            return Mapper.Map<IEnumerable<Order>>(result);
        }

        public async Task<RequestResponse> Withdraw(string quoteSymbol, string baseSymbol, decimal amount, string address)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Deposit>> GetDepositHistory(string symbol)
        {
            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.DepositHistory, true);

            var history = (JArray)result["depositList"];

            return Mapper.Map<IEnumerable<Deposit>>(history);
        }

        public async Task<IEnumerable<Withdraw>> GetWithdrawHistory(string symbol)
        {
            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.WithdrawHistory, true);

            var history = (JArray)result["withdrawList"];

            return Mapper.Map<IEnumerable<Withdraw>>(history);
        }

        #endregion
    }
}
