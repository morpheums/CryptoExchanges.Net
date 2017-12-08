using System.Collections.Generic;

namespace CryptoExchanges.Net.Models.Market
{
    public class AccountInfo
    {
        public int MakerCommission { get; set; }
        public int TakerCommission { get; set; }
        public int BuyerCommission { get; set; }
        public int SellerCommission { get; set; }
        public bool CanTrade { get; set; }
        public bool CanWithdraw { get; set; }
        public bool CanDeposit { get; set; }
        public IEnumerable<Balance> Balances { get; set; }
    }
    public class Balance
    {
        public string Asset { get; set; }
        public decimal Free { get; set; }
        public decimal Locked { get; set; }
    }
}
