using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoExchanges.Net.Domain.Providers
{
    public interface IApiCredentialsProvider
    {
        /// <summary>
        /// Represents the key that identifies the Exchange.
        /// </summary>
        string ExchangeName { get; }

        /// <summary>
        /// Gets the exchange credentials (Key and Secret).
        /// </summary>
        /// <param name="apiKey">Key to set for the exchange.</param>
        /// <param name="apiSecret">Secret to set for the exchange.</param>
        ApiCredentials GetCredentials();

        /// <summary>
        /// Gets the exchange credentials (Key and Secret).
        /// </summary>
        /// <param name="apiKey">Key to set for the exchange.</param>
        /// <param name="apiSecret">Secret to set for the exchange.</param>
        void SetCredentials();
    }
}
