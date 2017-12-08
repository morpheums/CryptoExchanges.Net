namespace CryptoExchanges.Net.Models.Account
{
    public class NewOrder
    {
        public string Symbol { get; set; }
        public int OrderId { get; set; }
        public string ClientOrderId { get; set; }
        public long TransactTime { get; set; }
    }
}
