using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoExchanges.Net.Enums;
using CryptoExchanges.Net.Models.Account;
using CryptoExchanges.Net.Models.Market;
using CryptoExchanges.Net.Domain;
using CryptoExchanges.Net.Binance.Clients.API;
using CryptoExchanges.Net.Binance.Constants;
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
        #endregion

        #region Properties
        /// <see cref="IExchangeClient.Key"/>>
        public string Key => "Binance";

        /// <see cref="IExchangeClient.Name"/>
        public string Name => "Binance Exchange";

        /// <see cref="IExchangeClient.Url"/>
        public string Url => "https://www.binance.com";

        /// <see cref="IExchangeClient.ApiVersion"/>
        public string ApiVersion => "v3";

        /// <see cref="IExchangeClient.SupportedOrderTypes"/>
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
        /// <see cref="IExchangeClient.SetCredentials(string, string)"/>
        public void SetCredentials(string apiKey, string apiSecret)
        {
            _apiClient.SetCredentials(Url, apiKey, apiSecret);
        }

        /// <see cref="IExchangeClient.HasCredentials"/>
        public bool HasCredentials()
        {
            return _apiClient.HasCredentials();
        }

        /// <see cref="IExchangeClient.ValidateNewOrder(NewOrderParams)"/>
        public void ValidateNewOrder(NewOrderParams newOrderParams)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Market Data
        /// <see cref="IExchangeClient.GetExchangeCurrenciesInfo"/>
        public IEnumerable<CurrencyInfo> GetExchangeCurrenciesInfo()
        {
            var result = _apiClient.Call<JObject>(ApiMethod.GET, Endpoints.ExchangeCurrencies, false);

            var symbolsInfo = (JArray)result.GetValue("symbols");

            return Mapper.Map<IEnumerable<CurrencyInfo>>(symbolsInfo);
        }

        /// <see cref="IExchangeClient.GetExchangeCurrenciesInfoAsync"/>
        public async Task<IEnumerable<CurrencyInfo>> GetExchangeCurrenciesInfoAsync()
        {
            var result = await _apiClient.CallAsync<JObject>(ApiMethod.GET, Endpoints.ExchangeCurrencies, false);

            var symbolsInfo = (JArray)result.GetValue("symbols");

            return Mapper.Map<IEnumerable<CurrencyInfo>>(symbolsInfo);
        }

        /// <see cref="IExchangeClient.GetAllTickersInfo"/>
        public IEnumerable<TickerInfo> GetAllTickersInfo()
        {
            var result = _apiClient.Call<JArray>(ApiMethod.GET, Endpoints.TickersInfo, false);

            return Mapper.Map<IEnumerable<TickerInfo>>(result);
        }

        /// <see cref="IExchangeClient.GetAllTickersInfoAsync"/>
        public async Task<IEnumerable<TickerInfo>> GetAllTickersInfoAsync()
        {
            var result = await _apiClient.CallAsync<JArray>(ApiMethod.GET, Endpoints.TickersInfo, false);

            return Mapper.Map<IEnumerable<TickerInfo>>(result);
        }

        /// <see cref="IExchangeClient.GetTickerInfo(string, string)"/>
        public TickerInfo GetTickerInfo(string quoteSymbol, string baseSymbol)
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

            var result = _apiClient.Call<JToken>(ApiMethod.GET, Endpoints.TickersInfo, false, $"symbol={pair.ToUpper()}");

            return Mapper.Map<TickerInfo>(result);
        }

        /// <see cref="IExchangeClient.GetTickerInfoAsync(string, string)"/>
        public async Task<TickerInfo> GetTickerInfoAsync(string quoteSymbol, string baseSymbol)
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

        /// <see cref="IExchangeClient.GetAllTickersPrice"/>
        public IEnumerable<TickerPrice> GetAllTickersPrice()
        {
            var result = _apiClient.Call<JArray>(ApiMethod.GET, Endpoints.TickerPrice, false);

            return Mapper.Map<IEnumerable<TickerPrice>>(result);
        }

        /// <see cref="IExchangeClient.GetAllTickersPriceAsync"/>
        public async Task<IEnumerable<TickerPrice>> GetAllTickersPriceAsync()
        {
            var result = await _apiClient.CallAsync<JArray>(ApiMethod.GET, Endpoints.TickerPrice, false);

            return Mapper.Map<IEnumerable<TickerPrice>>(result);
        }

        /// <see cref="IExchangeClient.GetTickerPrice(string, string)"/>
        public TickerPrice GetTickerPrice(string quoteSymbol, string baseSymbol)
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

            var result = _apiClient.Call<JToken>(ApiMethod.GET, Endpoints.TickerPrice, false, $"symbol={pair.ToUpper()}");

            return Mapper.Map<TickerPrice>(result);
        }

        /// <see cref="IExchangeClient.GetTickerPriceAsync(string, string)"/>
        public async Task<TickerPrice> GetTickerPriceAsync(string quoteSymbol, string baseSymbol)
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

        /// <see cref="IExchangeClient.GetOrderBook(string, string)(string, string)"/>
        public OrderBook GetOrderBook(string quoteSymbol, string baseSymbol)
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

            var result = _apiClient.Call<JObject>(ApiMethod.GET, Endpoints.OrderBook, false, $"symbol={pair.ToUpper()}&limit=20");

            return Mapper.Map<OrderBook>(result);
        }

        /// <see cref="IExchangeClient.GetOrderBookAsync(string, string)(string, string)"/>
        public async Task<OrderBook> GetOrderBookAsync(string quoteSymbol, string baseSymbol)
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

        /// <see cref="IExchangeClient.PostNewOrder(NewOrderParams)(NewOrderParams)"/>
        public RequestResponse PostNewOrder(NewOrderParams newOrderParams)
        {
            throw new NotImplementedException();
        }

        /// <see cref="IExchangeClient.PostNewOrderAsync(NewOrderParams)(NewOrderParams)"/>
        public async Task<RequestResponse> PostNewOrderAsync(NewOrderParams newOrderParams)
        {
            throw new NotImplementedException();
        }

        /// <see cref="IExchangeClient.GetAccoungBalance()"/>
        public IEnumerable<AssetBalance> GetAccoungBalance()
        {
            var result = _apiClient.Call<JObject>(ApiMethod.GET, Endpoints.AccountInformation, true);

            var balances = (JArray)result.GetValue("balances");

            return Mapper.Map<IEnumerable<AssetBalance>>(balances);
        }

        /// <see cref="IExchangeClient.GetAccoungBalanceAsync()"/>
        public async Task<IEnumerable<AssetBalance>> GetAccoungBalanceAsync()
        {
            var result = await _apiClient.CallAsync<JObject>(ApiMethod.GET, Endpoints.AccountInformation, true);

            var balances = (JArray)result.GetValue("balances");

            return Mapper.Map<IEnumerable<AssetBalance>>(balances);
        }

        /// <see cref="IExchangeClient.GetOrder(string, string, string)(string, string, string)"/>
        public Order GetOrder(string orderId, string quoteSymbol, string baseSymbol)
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

            var result = _apiClient.Call<JObject>(ApiMethod.GET, Endpoints.QueryOrder, true, args);

            return Mapper.Map<Order>(result);
        }

        /// <see cref="IExchangeClient.GetOrderAsync(string, string, string)(string, string, string)"/>
        public async Task<Order> GetOrderAsync(string orderId, string quoteSymbol, string baseSymbol)
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

        /// <see cref="IExchangeClient.CancelOrder(string, string, string)(string, string, string)"/>
        public RequestResponse CancelOrder(string orderId, string quoteSymbol = null, string baseSymbol = null)
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

            var result = _apiClient.Call<JObject>(ApiMethod.GET, Endpoints.QueryOrder, true, args);

            return new RequestResponse();
        }

        /// <see cref="IExchangeClient.CancelOrderAsync(string, string, string)(string, string, string)"/>
        public async Task<RequestResponse> CancelOrderAsync(string orderId, string quoteSymbol = null, string baseSymbol = null)
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

        /// <see cref="IExchangeClient.GetCurrentOpenOrders(string, string)(string, string)"/>
        public IEnumerable<Order> GetCurrentOpenOrders(string quoteSymbol, string baseSymbol)
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

            var result = _apiClient.Call<JToken>(ApiMethod.GET, Endpoints.CurrentOpenOrders, true, $"symbol={pair.ToUpper()}");

            return Mapper.Map<IEnumerable<Order>>(result);
        }

        /// <see cref="IExchangeClient.GetCurrentOpenOrdersAsync(string, string)(string, string)"/>
        public async Task<IEnumerable<Order>> GetCurrentOpenOrdersAsync(string quoteSymbol, string baseSymbol)
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

        /// <see cref="IExchangeClient.GetAllOrders(string, string)(string, string)"/>
        public IEnumerable<Order> GetAllOrders(string quoteSymbol, string baseSymbol)
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

            var result = _apiClient.Call<JToken>(ApiMethod.GET, Endpoints.AllOrders, true, $"symbol={pair.ToUpper()}");

            return Mapper.Map<IEnumerable<Order>>(result);
        }

        /// <see cref="IExchangeClient.GetAllOrdersAsync(string, string)(string, string)"/>
        public async Task<IEnumerable<Order>> GetAllOrdersAsync(string quoteSymbol, string baseSymbol)
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

        /// <see cref="IExchangeClient.Withdraw(string, string, decimal, string)(string, string, decimal, string)"/>
        public RequestResponse Withdraw(string quoteSymbol, string baseSymbol, decimal amount, string address)
        {
            throw new NotImplementedException();
        }

        /// <see cref="IExchangeClient.WithdrawAsync(string, string, decimal, string)(string, string, decimal, string)"/>
        public async Task<RequestResponse> WithdrawAsync(string quoteSymbol, string baseSymbol, decimal amount, string address)
        {
            throw new NotImplementedException();
        }

        /// <see cref="IExchangeClient.GetDepositHistory(string)(string)"/>
        public IEnumerable<Deposit> GetDepositHistory(string symbol)
        {
            var result = _apiClient.Call<JToken>(ApiMethod.GET, Endpoints.DepositHistory, true);

            var history = (JArray)result["depositList"];

            return Mapper.Map<IEnumerable<Deposit>>(history);
        }

        /// <see cref="IExchangeClient.GetDepositHistoryAsync(string)(string)"/>
        public async Task<IEnumerable<Deposit>> GetDepositHistoryAsync(string symbol)
        {
            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.DepositHistory, true);

            var history = (JArray)result["depositList"];

            return Mapper.Map<IEnumerable<Deposit>>(history);
        }

        /// <see cref="IExchangeClient.GetWithdrawHistory(string)(string)"/>
        public IEnumerable<Withdraw> GetWithdrawHistory(string symbol)
        {
            var result = _apiClient.Call<JToken>(ApiMethod.GET, Endpoints.WithdrawHistory, true);

            var history = (JArray)result["withdrawList"];

            return Mapper.Map<IEnumerable<Withdraw>>(history);
        }

        /// <see cref="IExchangeClient.GetWithdrawHistoryAsync(string)(string)"/>
        public async Task<IEnumerable<Withdraw>> GetWithdrawHistoryAsync(string symbol)
        {
            var result = await _apiClient.CallAsync<JToken>(ApiMethod.GET, Endpoints.WithdrawHistory, true);

            var history = (JArray)result["withdrawList"];

            return Mapper.Map<IEnumerable<Withdraw>>(history);
        }
        #endregion
    }
}
