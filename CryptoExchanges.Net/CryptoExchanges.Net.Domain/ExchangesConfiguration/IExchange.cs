using CryptoExchanges.Net.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Domain.Exchanges
{
    public interface IExchange
    {
        /// <summary>
        /// Represents the key that identifies the Exchange.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Represents the Name of the Exchange.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Represents the URL of the API.
        /// </summary>
        string Url { get; }

        /// <summary>
        /// Key used to authenticate within the API.
        /// </summary>
        string ApiKey { get; }

        /// <summary>
        /// API secret used to signed API calls.
        /// </summary>
        string ApiSecret { get; }
    }
}
