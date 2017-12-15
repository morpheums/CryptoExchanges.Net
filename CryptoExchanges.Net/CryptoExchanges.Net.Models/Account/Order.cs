using CryptoExchanges.Net.Models.Enums;
using System;

namespace CryptoExchanges.Net.Models.Account
{
    public class Order
    {
        public string OrderId { get; set; }
        public string Symbol { get; set; }
        public string Type { get; set; }
        public string Side { get; set; }
        public string TimeInForce { get; set; }
        public string Status { get; set; }
        public decimal Price { get; set; }
        public decimal StopPrice { get; set; }
        public decimal OriginalQuantity { get; set; }
        public decimal ExecutedQuantity { get; set; }
        public DateTime Date { get; set; }
    }
}
