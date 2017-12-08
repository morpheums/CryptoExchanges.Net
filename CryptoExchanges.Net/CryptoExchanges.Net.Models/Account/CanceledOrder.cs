namespace CryptoExchanges.Net.Models.Account
{
    public class CanceledOrder
    {
        public string Symbol { get; set; }
        public int OrderId { get; set; }
        public string ClientOrderId { get; set; }
        public string OrigClientOrderId { get; set; }
    }
}
