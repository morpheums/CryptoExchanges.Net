namespace CryptoExchanges.Net.Models.Account
{
    public class Order
    {
        public string Symbol { get; set; }
        public int OrderId { get; set; }
        public string ClientOrderId { get; set; }
        public decimal Price { get; set; }
        public decimal OrigQty { get; set; }
        public decimal ExecutedQty { get; set; }
        public string Status { get; set; }
        public string TimeInForce { get; set; }
        public string Type { get; set; }
        public string Side { get; set; }
        public decimal StopPrice { get; set; }
        public decimal IcebergQty { get; set; }
        public long Time { get; set; }
    }
}
