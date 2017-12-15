using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Models.Enums
{
    /// <summary>
    /// Different statuses of an order.
    /// </summary>
    public enum OrderStatus
    {
        OPEN,
        FILLED,
        CANCELED
    }
}
