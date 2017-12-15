using System.Collections.Generic;

namespace CryptoExchanges.Net.Models.Market
{
    public class AssetBalance
    {
        public string Asset { get; set; }
        public decimal Free { get; set; }
        public decimal Locked { get; set; }
    }
}
