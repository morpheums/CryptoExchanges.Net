using CryptoExchanges.Net.Models.Market;
using System.Collections.Generic;

namespace CryptoExchanges.Net.Bittrex.CustomParser
{
    /// <summary>
    /// Class to parse some specific entities.
    /// </summary>
    public interface IBittrexCustomParser
    {
        /// <summary>
        /// Gets the orderbook data and generates an OrderBook object.
        /// </summary>
        /// <param name="orderBookData">Dynamic containing the orderbook data.</param>
        /// <returns></returns>
        OrderBook GetParsedOrderBook(dynamic orderBookData);

        /// <summary>
        /// Gets the candlestick data and generates an Candlestick object.
        /// </summary>
        /// <param name="candlestickData">Dynamic containing the candlestick data.</param>
        /// <returns></returns>
        IEnumerable<Candlestick> GetParsedCandlestick(dynamic candlestickData);
    }
}
