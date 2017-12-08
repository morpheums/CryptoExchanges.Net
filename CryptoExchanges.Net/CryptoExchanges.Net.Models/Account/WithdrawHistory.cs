using System.Collections.Generic;

namespace CryptoExchanges.Net.Models.Account
{
    public class WithdrawHistory
    {
        public IEnumerable<Deposit> WithdrawList { get; set; }
        public bool Success { get; set; }
    }

    public class Withdraw
    {
        public decimal Amount { get; set; }
        public string Address { get; set; }
        public string TxId { get; set; }
        public string Asset { get; set; }
        public long ApplyTime { get; set; }
        public int Status { get; set; }
    }
}
