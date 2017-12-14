using CryptoExchanges.Net.Enums;
using CryptoExchanges.Net.Models.Account;
using CryptoExchanges.Net.Models.Market;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Domain
{
    /// <summary>
    /// Interface that defines the behaviour of the exchanges clients.
    /// </summary>
    public interface IExchangeClient
    {
        #region Properties
        /// <summary>
        /// Represents the key that identifies the Exchange.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Represents the Name of the Exchange.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Represents the URL of the API.
        /// </summary>
        string Url { get; }

        /// <summary>
        /// Represents the current version of the API.
        /// </summary>
        string ApiVersion { get; }
        #endregion

        #region Methods and Functions
        /// <summary>
        /// States if the credentials (Key and Secret) were setted.
        /// </summary>
        bool HasCredentials();

        /// <summary>
        /// Sets the exchange credentials (Key and Secret).
        /// </summary>
        /// <param name="apiKey">Key to set for the exchange.</param>
        /// <param name="apiSecret">Secret to set for the exchange.</param>
        void SetCredentials(string apiKey, string apiSecret);
        #endregion

        #region Market Data Methods

        /// <summary>
        /// Get exchange markets information and price limits.
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<CurrencyInfo>> GetExchangeCurrenciesInfo();

        /// <summary>
        /// Get 24 hour price statistics for all tickers.
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<TickerInfo>> GetAllTickersInfo();

        /// <summary>
        /// Get 24 hour price statistics for an specific ticker.
        /// </summary>
        /// <param name="getTickerInfoParams">Ticker symbol to look for.</param>
        /// <returns></returns>
        Task<TickerInfo> GetTickerInfo(string quoteSymbol, string baseSymbol);

        /// <summary>
        /// Get latest price for all tickers.
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<TickerPrice>> GetAllTickersPrice();

        /// <summary>
        ///  Latest price for an specific ticker.
        /// </summary>
        /// <param name="getTickerInfoParams">Ticker symbol to look for.</param>
        /// <returns></returns>
        Task<TickerPrice> GetTickerPrice(string quoteSymbol, string baseSymbol);

        /// <summary>
        /// Get order book for a particular symbol.
        /// </summary>
        /// <param name="symbolParams">Ticker symbol to look for.</param>
        /// <returns></returns>
        Task<OrderBook> GetOrderBook(string quoteSymbol, string baseSymbol);
        #endregion

        #region Account Information Methods
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
        Task<NewOrder> PostNewOrder(string symbol, decimal quantity, decimal price, OrderSide side, OrderType orderType = OrderType.LIMIT, TimeInForce timeInForce = TimeInForce.GTC, decimal icebergQty = 0m, long recvWindow = 6000000);

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
        Task<dynamic> PostNewOrderTest(string symbol, decimal quantity, decimal price, OrderSide side, OrderType orderType = OrderType.LIMIT, TimeInForce timeInForce = TimeInForce.GTC, decimal icebergQty = 0m, long recvWindow = 6000000);

        /// <summary>
        /// Check an order's status.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="orderId">Id of the order to retrieve.</param>
        /// <param name="origClientOrderId">origClientOrderId of the order to retrieve.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<Order> GetOrder(string symbol, long? orderId = null, string origClientOrderId = null, long recvWindow = 6000000);

        /// <summary>
        /// Cancel an active order.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="orderId">Id of the order to cancel.</param>
        /// <param name="origClientOrderId">origClientOrderId of the order to cancel.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<CanceledOrder> CancelOrder(string symbol, long? orderId = null, string origClientOrderId = null, long recvWindow = 6000000);

        /// <summary>
        /// Get all open orders on a symbol.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<IEnumerable<Order>> GetCurrentOpenOrders(string symbol, long recvWindow = 6000000);

        /// <summary>
        /// Get all account orders; active, canceled, or filled.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="orderId">If is set, it will get orders >= that orderId. Otherwise most recent orders are returned.</param>
        /// <param name="limit">Limit of records to retrieve.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<IEnumerable<Order>> GetAllOrders(string symbol, long? orderId = null, int limit = 500, long recvWindow = 6000000);

        /// <summary>
        /// Get current account information.
        /// </summary>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<AccountInfo> GetAccountInfo(long recvWindow = 6000000);

        /// <summary>
        /// Get trades for a specific account and symbol.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<IEnumerable<Trade>> GetTradeList(string symbol, long recvWindow = 6000000);

        /// <summary>
        /// Submit a withdraw request.
        /// </summary>
        /// <param name="asset">Asset to withdraw.</param>
        /// <param name="amount">Amount to withdraw.</param>
        /// <param name="address">Address where the asset will be deposited.</param>
        /// <param name="addressName">Address name.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<WithdrawResponse> Withdraw(string asset, decimal amount, string address, string addressName = "", long recvWindow = 6000000);

        /// <summary>
        /// Fetch deposit history.
        /// </summary>
        /// <param name="asset">Asset you want to see the information for.</param>
        /// <param name="status">Deposit status.</param>
        /// <param name="startTime">Start time. </param>
        /// <param name="endTime">End time.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<DepositHistory> GetDepositHistory(string asset, DepositStatus? status = null, DateTime? startTime = null, DateTime? endTime = null, long recvWindow = 6000000);

        /// <summary>
        /// Fetch withdraw history.
        /// </summary>
        /// <param name="asset">Asset you want to see the information for.</param>
        /// <param name="status">Withdraw status.</param>
        /// <param name="startTime">Start time. </param>
        /// <param name="endTime">End time.</param>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        Task<WithdrawHistory> GetWithdrawHistory(string asset, WithdrawStatus? status = null, DateTime? startTime = null, DateTime? endTime = null, long recvWindow = 6000000);
        #endregion
    }
}
