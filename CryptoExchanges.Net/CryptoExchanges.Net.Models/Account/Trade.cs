namespace CryptoExchanges.Net.Models.Account
{
    public class Trade
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Commission { get; set; }
        public string CommissionAsset { get; set; }
        public long Time { get; set; }
        public bool IsBuyer { get; set; }
        public bool IsMaker { get; set; }
        public bool IsBestMatch { get; set; }
    }
}
