namespace CryptoExchanges.Net.Binance.Constants
{
    /// <summary>
    /// API endpoints of the exchange.
    /// </summary>
    public static class Endpoints
    {
        #region Market Data Endpoints
        public const string ExchangeCurrencies = "/api/v1/exchangeInfo";
        public const string OrderBook = "/api/v1/depth";
        public const string TickerPrice = "/api/v3/ticker/price";
        public const string TickersInfo = "/api/v1/ticker/24hr";
        #endregion

        #region Account Endpoints
        public const string NewOrder = "/api/v3/order";
        public const string NewOrderTest = "/api/v3/order/test";
        public const string QueryOrder = "/api/v3/order";
        public const string CancelOrder = "/api/v3/order";
        public const string CurrentOpenOrders = "/api/v3/openOrders";
        public const string AllOrders = "/api/v3/allOrders";
        public const string AccountInformation = "/api/v3/account";
        public const string TradeList = "/api/v3/myTrades";

        public const string Withdraw = "/wapi/v3/withdraw.html";
        public const string DepositHistory = "/wapi/v3/depositHistory.html";
        public const string WithdrawHistory = "/wapi/v3/withdrawHistory.html";
        #endregion
    }
}
