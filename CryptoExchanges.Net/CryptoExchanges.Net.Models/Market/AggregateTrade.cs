namespace CryptoExchanges.Net.Models.Market
{
    public class AggregateTrade
    {
        public int AggregateTradeId { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public int FirstTradeId { get; set; }
        public int LastTradeId { get; set; }
        public long TimeStamp { get; set; }
        public bool BuyerIsMaker { get; set; }
        public bool BestPriceMatch { get; set; }
    }
}
