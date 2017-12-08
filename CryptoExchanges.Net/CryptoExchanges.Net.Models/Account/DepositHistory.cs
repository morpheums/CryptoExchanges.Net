using System.Collections.Generic;

namespace CryptoExchanges.Net.Models.Account
{
    public class Deposit
    {
        public long InsertTime { get; set; }
        public decimal Amount { get; set; }
        public string Asset { get; set; }
        public int Status { get; set; }
    }

    public class DepositHistory
    {
        public IEnumerable<Deposit> DepositList { get; set; }
        public bool Success { get; set; }
    }
}
