namespace CryptoExchanges.Net.Binance.Constants
{
    /// <summary>
    /// API endpoints of the exchange.
    /// </summary>
    public static class Endpoints
    {
        #region Market Data Endpoints
        public const string OrderBook = "/api/v1/depth";
        public const string AggregateTrades = "/api/v1/aggTrades";
        public const string Candlesticks = "/api/v1/klines";
        public const string TickerPriceChange24H = "/api/v1/ticker/24hr";
        public const string AllPrices = "/api/v1/ticker/allPrices";
        public const string OrderBookTicker = "/api/v1/ticker/allBookTickers";
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

        public const string Withdraw = "/wapi/v1/withdraw.html";
        public const string DepositHistory = "/wapi/v1/getDepositHistory.html";
        public const string WithdrawHistory = "/wapi/v1/getWithdrawHistory.html";
        #endregion
    }
}
