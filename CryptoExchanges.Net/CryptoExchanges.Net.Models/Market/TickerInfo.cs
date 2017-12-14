namespace CryptoExchanges.Net.Models.Market
{
    public class TickerInfo
    {
        public string Pair { get; set; }
        public string Symbol { get; set; }
        public string BaseSymbol { get; set; }
        public decimal BidPrice { get; set; }
        public decimal AskPrice { get; set; }
        public decimal LastPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal Volume { get; set; }
    }
}
