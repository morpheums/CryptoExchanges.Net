using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Models.Market
{
    public class TickerPrice
    {
        public string Pair { get; set; }
        public string Symbol { get; set; }
        public string BaseSymbol { get; set; }
        public decimal Price { get; set; }
    }
}
