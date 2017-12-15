using CryptoExchanges.Net.Enums;
using CryptoExchanges.Net.Models.Account;
using CryptoExchanges.Net.Models.Common;
using CryptoExchanges.Net.Models.Enums;
using CryptoExchanges.Net.Models.Market;
using CryptoExchanges.Net.Models.Params;
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

        /// <summary>
        /// Specifies which are the order types supported by the exchange.
        /// </summary>
        List<OrderType> SupportedOrderTypes { get; }
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

        /// <summary>
        /// Validates if a new order request is valid before posting it.
        /// </summary>
        /// <param name="newOrderParams">Params to post a new order</param>
        void ValidateNewOrder(NewOrderParams newOrderParams);
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
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
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
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
        /// <returns></returns>
        Task<TickerPrice> GetTickerPrice(string quoteSymbol, string baseSymbol);

        /// <summary>
        /// Get order book for a particular symbol.
        /// </summary>
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
        /// <returns></returns>
        Task<OrderBook> GetOrderBook(string quoteSymbol, string baseSymbol);
        #endregion

        #region Account Information Methods
        /// <summary>
        /// Get current account balances.
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<AssetBalance>> GetAccoungBalance();

        /// <summary>
        /// Send in a new order.
        /// </summary>
        /// <param name="newOrderParams">Params to post a new order.</param>
        /// <returns></returns>
        Task<RequestResponse> PostNewOrder(NewOrderParams newOrderParams);

        /// <summary>
        /// Check an order's status.
        /// </summary>
        /// <param name="orderId">Id of the order to retrieve.</param>
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
        /// <returns></returns>
        Task<Order> GetOrder(string orderId, string quoteSymbol, string baseSymbol);

        /// <summary>
        /// Cancel an active order.
        /// </summary>
        /// <param name="orderId">Id of the order to retrieve.</param>
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
        /// <returns></returns>
        Task<RequestResponse> CancelOrder(string orderId, string quoteSymbol = null, string baseSymbol = null);

        /// <summary>
        /// Get all open orders on a symbol.
        /// </summary>
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
        /// <returns></returns>
        Task<IEnumerable<Order>> GetCurrentOpenOrders(string quoteSymbol, string baseSymbol);

        /// <summary>
        /// Get all account orders; active, canceled, or filled.
        /// </summary>
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
        /// <returns></returns>
        Task<IEnumerable<Order>> GetAllOrders(string quoteSymbol, string baseSymbol);

        ///// <summary>
        ///// Get trades for a specific account and symbol.
        ///// </summary>
        ///// <param name="quoteSymbol">Quote symbol to look for.</param>
        ///// <param name="baseSymbol">Base symbol to look for.</param>
        ///// <returns></returns>
        //Task<IEnumerable<Trade>> GetTradeList(string quoteSymbol, string baseSymbol);

        /// <summary>
        /// Submit a withdraw request.
        /// </summary>
        /// <param name="quoteSymbol">Quote symbol to look for.</param>
        /// <param name="baseSymbol">Base symbol to look for.</param>
        /// <param name="amount">Amount to withdraw.</param>
        /// <param name="address">Address where the asset will be deposited.</param>
        /// <returns></returns>
        Task<RequestResponse> Withdraw(string quoteSymbol, string baseSymbol, decimal amount, string address);

        /// <summary>
        /// Fetch deposit history.
        /// </summary>
        /// <param name="symbol">Asset you want to see the information for.</param>
        /// <returns></returns>
        Task<IEnumerable<Deposit>> GetDepositHistory(string symbol);

        /// <summary>
        /// Fetch withdraw history.
        /// </summary>
        /// <param name="symbol">Asset you want to see the information for.</param>
        /// <returns></returns>
        Task<IEnumerable<Withdraw>> GetWithdrawHistory(string symbol);
        #endregion
    }
}
