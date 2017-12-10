﻿using CryptoExchanges.Net.Domain.Clients;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoExchanges.Net.Domain.Enums;
using CryptoExchanges.Net.Enums;
using CryptoExchanges.Net.Models.Account;
using CryptoExchanges.Net.Models.Market;
using CryptoExchanges.Net.Logic.Clients.Exchanges.Base;
using CryptoExchanges.Net.Logic.CustomParsers;
using CryptoExchanges.Net.Logic.Utils;
using CryptoExchanges.Net.Domain.CustomParsers;

namespace CryptoExchanges.Net.Logic.Clients.Exchanges
{
    public class BinanceClient : ExchangeClientBase, IExchangeClient
    {
        #region Properties
        private IBinanceCustomParser _binanceCustomParser { get; set; }
        private readonly string _exchangeKey;
        #endregion

        /// <summary>
        /// ctor.
        /// </summary>
        /// <param name="apiClient">API client to be used for API calls.</param>
        public BinanceClient(IApiClient apiClient, IBinanceCustomParser binanceCustomParser) : base(apiClient)
        {
            _exchangeKey = "Binance";
            _binanceCustomParser = binanceCustomParser;
        }

        #region Market Data
        /// <summary>
        /// Get order book for a particular symbol.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="limit">Limit of records to retrieve.</param>
        /// <returns></returns>
        public async Task<OrderBook> GetOrderBook(string symbol, int limit = 100)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var result = await _apiClient.CallAsync<dynamic>(ApiMethod.GET,Endpoints.OrderBook, false, $"symbol={symbol.ToUpper()}&limit={limit}");

            return _binanceCustomParser.GetParsedOrderBook(result);
        }

        /// <summary>
        /// Get compressed, aggregate trades. Trades that fill at the time, from the same order, with the same price will have the quantity aggregated.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="limit">Limit of records to retrieve.</param>
        /// <returns></returns>
        public async Task<IEnumerable<AggregateTrade>> GetAggregateTrades(string symbol, int limit = 500)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var result = await _apiClient.CallAsync<IEnumerable<AggregateTrade>>(ApiMethod.GET,Endpoints.AggregateTrades, false, $"symbol={symbol.ToUpper()}&limit={limit}");

            return result;
        }

        /// <summary>
        /// Kline/candlestick bars for a symbol. Klines are uniquely identified by their open time.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <param name="interval">Time interval to retreive.</param>
        /// <param name="limit">Limit of records to retrieve.</param>
        /// <returns></returns>
        public async Task<IEnumerable<Candlestick>> GetCandleSticks(string symbol, TimeInterval interval, int limit = 500)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var result = await _apiClient.CallAsync<dynamic>(ApiMethod.GET,Endpoints.Candlesticks, false, $"symbol={symbol.ToUpper()}&interval={interval.GetDescription()}&limit={limit}");

            return _binanceCustomParser.GetParsedCandlestick(result);
        }

        /// <summary>
        /// 24 hour price change statistics.
        /// </summary>
        /// <param name="symbol">Ticker symbol.</param>
        /// <returns></returns>
        public async Task<PriceChangeInfo> GetPriceChange24H(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("symbol cannot be empty. ", "symbol");
            }

            var result = await _apiClient.CallAsync<PriceChangeInfo>(ApiMethod.GET,Endpoints.TickerPriceChange24H, false, $"symbol={symbol.ToUpper()}");

            return result;
        }

        /// <summary>
        /// Latest price for all symbols.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<SymbolPrice>> GetAllPrices()
        {
            var result = await _apiClient.CallAsync<IEnumerable<SymbolPrice>>(ApiMethod.GET,Endpoints.AllPrices, false);

            return result;
        }

        /// <summary>
        /// Best price/qty on the order book for all symbols.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<OrderBookTicker>> GetOrderBookTicker()
        {
            var result = await _apiClient.CallAsync<IEnumerable<OrderBookTicker>>(ApiMethod.GET,Endpoints.OrderBookTicker, false);

            return result;
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
            var result = await _apiClient.CallAsync<NewOrder>(ApiMethod.POST,Endpoints.NewOrder, true, args);

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
            var result = await _apiClient.CallAsync<dynamic>(ApiMethod.POST,Endpoints.NewOrderTest, true, args);

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

            var result = await _apiClient.CallAsync<Order>(ApiMethod.GET,Endpoints.QueryOrder, true, args);

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

            var result = await _apiClient.CallAsync<CanceledOrder>(ApiMethod.DELETE,Endpoints.CancelOrder, true, args);

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

            var result = await _apiClient.CallAsync<IEnumerable<Order>>(ApiMethod.GET,Endpoints.CurrentOpenOrders, true, $"symbol={symbol.ToUpper()}&recvWindow={recvWindow}");

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

            var result = await _apiClient.CallAsync<IEnumerable<Order>>(ApiMethod.GET,Endpoints.AllOrders, true, $"symbol={symbol.ToUpper()}&limit={limit}&recvWindow={recvWindow}" + (orderId.HasValue ? $"&orderId={orderId.Value}" : ""));

            return result;
        }

        /// <summary>
        /// Get current account information.
        /// </summary>
        /// <param name="recvWindow">Specific number of milliseconds the request is valid for.</param>
        /// <returns></returns>
        public async Task<AccountInfo> GetAccountInfo(long recvWindow = 6000000)
        {
            var result = await _apiClient.CallAsync<AccountInfo>(ApiMethod.GET,Endpoints.AccountInformation, true, $"recvWindow={recvWindow}");

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

            var result = await _apiClient.CallAsync<IEnumerable<Trade>>(ApiMethod.GET,Endpoints.TradeList, true, $"symbol={symbol.ToUpper()}&recvWindow={recvWindow}");

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

            var result = await _apiClient.CallAsync<WithdrawResponse>(ApiMethod.POST,Endpoints.Withdraw, true, args);

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

            var result = await _apiClient.CallAsync<DepositHistory>(ApiMethod.POST,Endpoints.DepositHistory, true, args);

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

            var result = await _apiClient.CallAsync<WithdrawHistory>(ApiMethod.POST,Endpoints.WithdrawHistory, true, args);

            return result;
        }
        #endregion
    }

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
