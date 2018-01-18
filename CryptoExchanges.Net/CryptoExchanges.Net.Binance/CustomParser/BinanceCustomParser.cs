using CryptoExchanges.Net.Models.Market;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace CryptoExchanges.Net.Binance.CustomParser
{
    /// <summary>
    /// Class to parse some specific entities.
    /// </summary>
    public class BinanceCustomParser: IBinanceCustomParser
    {
        /// <summary>
        /// Gets the orderbook data and generates an OrderBook object.
        /// </summary>
        /// <param name="orderBookData">Dynamic containing the orderbook data.</param>
        /// <returns></returns>
        public OrderBook GetParsedOrderBook(dynamic orderBookData)
        {
            var result = new OrderBook();

            var bids = new List<OrderBookOffer>();
            var asks = new List<OrderBookOffer>();

            foreach (JToken item in ((JArray)orderBookData.bids).ToArray())
            {
                bids.Add(new OrderBookOffer() { Price = decimal.Parse(item[0].ToString()), Quantity = decimal.Parse(item[1].ToString()) });
            }

            foreach (JToken item in ((JArray)orderBookData.asks).ToArray())
            {
                asks.Add(new OrderBookOffer() { Price = decimal.Parse(item[0].ToString()), Quantity = decimal.Parse(item[1].ToString()) });
            }

            result.Bids = bids;
            result.Asks = asks;

            return result;
        }
    }
}
