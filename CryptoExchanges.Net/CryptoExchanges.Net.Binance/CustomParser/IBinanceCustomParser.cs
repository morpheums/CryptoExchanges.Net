using CryptoExchanges.Net.Models.Market;
using System.Collections.Generic;

namespace CryptoExchanges.Net.Binance.CustomParser
{
    /// <summary>
    /// Class to parse some specific entities.
    /// </summary>
    public interface IBinanceCustomParser
    {
        /// <summary>
        /// Gets the orderbook data and generates an OrderBook object.
        /// </summary>
        /// <param name="orderBookData">Dynamic containing the orderbook data.</param>
        /// <returns></returns>
        OrderBook GetParsedOrderBook(dynamic orderBookData);
    }
}
