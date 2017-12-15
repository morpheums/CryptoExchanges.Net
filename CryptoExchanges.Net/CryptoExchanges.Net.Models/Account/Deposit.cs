using System;
using System.Collections.Generic;

namespace CryptoExchanges.Net.Models.Account
{
    public class Deposit
    {
        public string Asset { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime Date { get; set; }
    }
}
