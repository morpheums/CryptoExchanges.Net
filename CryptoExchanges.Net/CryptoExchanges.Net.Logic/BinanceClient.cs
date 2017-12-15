using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoExchanges.Net.Domain.Enums;
using CryptoExchanges.Net.Enums;
using CryptoExchanges.Net.Models.Account;
using CryptoExchanges.Net.Models.Market;
using CryptoExchanges.Net.Domain;
using CryptoExchanges.Net.Binance.Clients.API;
using CryptoExchanges.Net.Binance.Constants;
using CryptoExchanges.Net.Binance.Utils;
using Newtonsoft.Json.Linq;
using AutoMapper;

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

        public List<OrderType> SupportedOrderTypes => new List<OrderType>() { OrderType.LIMIT, OrderType.MARKET,OrderType.STOP_LOSS, OrderType.STOP_LOSS_LIMIT, OrderType.TAKE_PROFIT, OrderType.TAKE_PROFIT_LIMIT};
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
        public bool HasCredentials() {
            return _apiClient.HasCredentials();
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
        /// <summary>
        /// Send in a new order.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="quantity">Quantity to transaction.</param>
        /// <param name="price">Price of the transaction.</param>
        /// <param name="orderType">Order type (LIMIT-MARKET).</param>
        /// <param name="side">Order side (BUY-SELL).</param>
        /// <param name="timeInForce">Indicates how long an order will remain active before it is executed or expires.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<NewOrder> PostNewOrder(string symbol, decimal quantity, decimal price, OrderSide side, OrderType orderType = OrderType.LIMIT, TimeInForce timeInForce = TimeInForce.GTC, decimal icebergQty = 0m, long recvWindow = 6000000)
        {
            //Validates that the order is valid.
            //ValidateOrderValue(symbol, orderType, price, quantity, icebergQty);

            var args = $"symbol={symbol.ToUpper()}&side={side}&type={orderType}&quantity={quantity}"
                + (orderType == OrderType.LIMIT ? $"&timeInForce={timeInForce}" : "")
                + (orderType == OrderType.LIMIT ? $"&price={price}" : "")
                + (icebergQty > 0m ? $"&icebergQty={icebergQty}" : "")
                + $"&recvWindow={recvWindow}";
            var result = await _apiClient.CallAsync<NewOrder>(ApiMethod.POST, Endpoints.NewOrder, true, args);

            return result;
        }

        /// <summary>
        /// Test new order creation and signature/recvWindow long. Creates and validates a new order but does not send it into the matching engine.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="quantity">Quantity to transaction.</param>
        /// <param name="price">Price of the transaction.</param>
        /// <param name="orderType">Order type (LIMIT-MARKET).</param>
        /// <param name="side">Order side (BUY-SELL).</param>
        /// <param name="timeInForce">Indicates how long an order will remain active before it is executed or expires.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<dynamic> PostNewOrderTest(string symbol, decimal quantity, decimal price, OrderSide side, OrderType orderType = OrderType.LIMIT, TimeInForce timeInForce = TimeInForce.GTC, decimal icebergQty = 0m, long recvWindow = 6000000)
        {
            //Validates that the order is valid.
            //ValidateOrderValue(symbol, orderType, price, quantity, icebergQty);

            var args = $"symbol={symbol.ToUpper()}&side={side}&type={orderType}&quantity={quantity}"
                + (orderType == OrderType.LIMIT ? $"&timeInForce={timeInForce}" : "")
                + (orderType == OrderType.LIMIT ? $"&price={price}" : "")
                + (icebergQty > 0m ? $"&icebergQty={icebergQty}" : "")
                + $"&recvWindow={recvWindow}";
            var result = await _apiClient.CallAsync<dynamic>(ApiMethod.POST, Endpoints.NewOrderTest, true, args);

            return result;
        }

        /// <summary>
        /// Check an order's status.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="orderId">Id of the order to retrieve.</param>
        /// <param name="origClientOrderId">origClientOrderId of the order to retrieve.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<Order> GetOrder(string symbol, long? orderId = null, string origClientOrderId = null, long recvWindow = 6000000)
        {
            var args = $"symbol={symbol.ToUpper()}&recvWindow={recvWindow}";

            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            if (orderId.HasValue)
            {
                args += $"&orderId={orderId.Value}";
            }
            else if (!string.IsNullOrWhiteSpace(origClientOrderId))
            {
                args += $"&origClientOrderId={origClientOrderId}";
            }
            else
            {
                throw new ArgumentException("Either orderId or origClientOrderId must be sent.");
            }

            var result = await _apiClient.CallAsync<Order>(ApiMethod.GET, Endpoints.QueryOrder, true, args);

            return result;
        }

        /// <summary>
        /// Cancel an active order.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="orderId">Id of the order to cancel.</param>
        /// <param name="origClientOrderId">origClientOrderId of the order to cancel.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<CanceledOrder> CancelOrder(string symbol, long? orderId = null, string origClientOrderId = null, long recvWindow = 6000000)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var args = $"symbol={symbol.ToUpper()}&recvWindow={recvWindow}";

            if (orderId.HasValue)
            {
                args += $"&orderId={orderId.Value}";
            }
            else if (string.IsNullOrWhiteSpace(origClientOrderId))
            {
                args += $"&origClientOrderId={origClientOrderId}";
            }
            else
            {
                throw new ArgumentException("Either orderId or origClientOrderId must be sent.");
            }

            var result = await _apiClient.CallAsync<CanceledOrder>(ApiMethod.DELETE, Endpoints.CancelOrder, true, args);

            return result;
        }

        /// <summary>
        /// Get all open orders on a symbol.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<IEnumerable<Order>> GetCurrentOpenOrders(string symbol, long recvWindow = 6000000)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var result = await _apiClient.CallAsync<IEnumerable<Order>>(ApiMethod.GET, Endpoints.CurrentOpenOrders, true, $"symbol={symbol.ToUpper()}&recvWindow={recvWindow}");

            return result;
        }

        /// <summary>
        /// Get all account orders; active, canceled, or filled.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="orderId">If is set, it will get orders >= that orderId. Otherwise most recent orders are returned.</param>
        /// <param name="limit">Limit of records to retrieve.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<IEnumerable<Order>> GetAllOrders(string symbol, long? orderId = null, int limit = 500, long recvWindow = 6000000)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var result = await _apiClient.CallAsync<IEnumerable<Order>>(ApiMethod.GET, Endpoints.AllOrders, true, $"symbol={symbol.ToUpper()}&limit={limit}&recvWindow={recvWindow}" + (orderId.HasValue ? $"&orderId={orderId.Value}" : ""));

            return result;
        }

        /// <summary>
        /// Get current account information.
        /// </summary>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<AccountInfo> GetAccountInfo(long recvWindow = 6000000)
        {
            var result = await _apiClient.CallAsync<AccountInfo>(ApiMethod.GET, Endpoints.AccountInformation, true, $"recvWindow={recvWindow}");

            return result;
        }

        /// <summary>
        /// Get trades for a specific account and symbol.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<IEnumerable<Trade>> GetTradeList(string symbol, long recvWindow = 6000000)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var result = await _apiClient.CallAsync<IEnumerable<Trade>>(ApiMethod.GET, Endpoints.TradeList, true, $"symbol={symbol.ToUpper()}&recvWindow={recvWindow}");

            return result;
        }

        /// <summary>
        /// Submit a withdraw request.
        /// </summary>
        /// <param name="asset">Asset to withdraw.</param>
        /// <param name="amount">Amount to withdraw.</param>
        /// <param name="address">Address where the asset will be deposited.</param>
        /// <param name="addressName">Address name.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<WithdrawResponse> Withdraw(string asset, decimal amount, string address, string addressName = "", long recvWindow = 6000000)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                throw new ArgumentException("asset cannot be empty. ", "asset");
            }
            if (amount <= 0m)
            {
                throw new ArgumentException("amount must be greater than zero.", "amount");
            }
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("address cannot be empty. ", "address");
            }

            var args = $"asset={asset.ToUpper()}&amount={amount}&address={address}"
              + (!string.IsNullOrWhiteSpace(addressName) ? $"&name={addressName}" : "")
              + $"&recvWindow={recvWindow}";

            var result = await _apiClient.CallAsync<WithdrawResponse>(ApiMethod.POST, Endpoints.Withdraw, true, args);

            return result;
        }

        /// <summary>
        /// Fetch deposit history.
        /// </summary>
        /// <param name="asset">Asset you want to see the information for.</param>
        /// <param name="status">Deposit status.</param>
        /// <param name="startTime">Start time. </param>
        /// <param name="endTime">End time.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<DepositHistory> GetDepositHistory(string asset, DepositStatus? status = null, DateTime? startTime = null, DateTime? endTime = null, long recvWindow = 6000000)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                throw new ArgumentException("asset cannot be empty. ", "asset");
            }

            var args = $"asset={asset.ToUpper()}"
              + (status.HasValue ? $"&status={(int)status}" : "")
              + (startTime.HasValue ? $"&startTime={Utilities.GenerateTimeStamp(startTime.Value)}" : "")
              + (endTime.HasValue ? $"&endTime={Utilities.GenerateTimeStamp(endTime.Value)}" : "")
              + $"&recvWindow={recvWindow}";

            var result = await _apiClient.CallAsync<DepositHistory>(ApiMethod.POST, Endpoints.DepositHistory, true, args);

            return result;
        }

        /// <summary>
        /// Fetch withdraw history.
        /// </summary>
        /// <param name="asset">Asset you want to see the information for.</param>
        /// <param name="status">Withdraw status.</param>
        /// <param name="startTime">Start time. </param>
        /// <param name="endTime">End time.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<WithdrawHistory> GetWithdrawHistory(string asset, WithdrawStatus? status = null, DateTime? startTime = null, DateTime? endTime = null, long recvWindow = 6000000)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                throw new ArgumentException("asset cannot be empty. ", "asset");
            }

            var args = $"asset={asset.ToUpper()}"
              + (status.HasValue ? $"&status={(int)status}" : "")
              + (startTime.HasValue ? $"&startTime={Utilities.GenerateTimeStamp(startTime.Value)}" : "")
              + (endTime.HasValue ? $"&endTime={Utilities.GenerateTimeStamp(endTime.Value)}" : "")
              + $"&recvWindow={recvWindow}";

            var result = await _apiClient.CallAsync<WithdrawHistory>(ApiMethod.POST, Endpoints.WithdrawHistory, true, args);

            return result;
        }

        public Task<NewOrder> PostNewOrder(string quoteSymbol, string baseSymbol, decimal quantity, decimal price, OrderSide side, OrderType orderType = OrderType.LIMIT, TimeInForce timeInForce = TimeInForce.GTC)
        {
            throw new NotImplementedException();
        }

        public Task<Order> GetOrder(string symbol, long? orderId = null, string origClientOrderId = null)
        {
            throw new NotImplementedException();
        }

        public Task<CanceledOrder> CancelOrder(string symbol, long? orderId = null, string origClientOrderId = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Order>> GetCurrentOpenOrders(string symbol)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Order>> GetAllOrders(string symbol, long? orderId = null, int limit = 500)
        {
            throw new NotImplementedException();
        }

        public Task<AccountInfo> GetAccountInfo()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Trade>> GetTradeList(string symbol)
        {
            throw new NotImplementedException();
        }

        public Task<WithdrawResponse> Withdraw(string asset, decimal amount, string address, string addressName = "")
        {
            throw new NotImplementedException();
        }

        public Task<DepositHistory> GetDepositHistory(string asset, DepositStatus? status = null, DateTime? startTime = null, DateTime? endTime = null)
        {
            throw new NotImplementedException();
        }

        public Task<WithdrawHistory> GetWithdrawHistory(string asset, WithdrawStatus? status = null, DateTime? startTime = null, DateTime? endTime = null)
        {
            throw new NotImplementedException();
        }

        public Task<NewOrder> PostNewOrder(string symbol, decimal quantity, decimal price, OrderSide side, OrderType orderType = OrderType.LIMIT, TimeInForce timeInForce = TimeInForce.GTC)
        {
            throw new NotImplementedException();
        }




        #endregion
    }
}
