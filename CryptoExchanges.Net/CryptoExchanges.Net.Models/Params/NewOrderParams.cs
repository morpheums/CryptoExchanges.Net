using CryptoExchanges.Net.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Models.Params
{
    public class NewOrderParams
    {
        string QuoteSymbol { get; set; }
        string BaseSymbol { get; set; }
        decimal Quantity { get; set; }
        decimal Price { get; set; }
        decimal StopPrice { get; set; }
        OrderSide Side { get; set; }
        OrderType OrderType { get; set; }
        TimeInForce TimeInForce { get; set; }
    }
}
