namespace CryptoExchanges.Net.Models.Market
{
    public class OrderBookTicker
    {
        public string Symbol { get; set; }
        public decimal BidPrice { get; set; }
        public decimal BidQuantity { get; set; }
        public decimal AskPrice { get; set; }
        public decimal AskQuantity { get; set; }
    }
}
