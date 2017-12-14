using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Models.Market
{
    public class CurrencyInfo
    {
        public string Pair { get; set; }
        public string Symbol { get; set; }
        public string BaseSymbol { get; set; }
        public decimal MinTradePrice { get; set; }
    }
}
