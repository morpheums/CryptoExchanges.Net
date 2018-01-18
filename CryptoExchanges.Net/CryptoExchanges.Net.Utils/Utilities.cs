using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace CryptoExchanges.Net.Utils
{
    /// <summary>
    /// Utility class for common tasks. 
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// Gets a HMACSHA256 signature based on the API Secret.
        /// </summary>
        /// <param name="apiSecret">Api secret used to generate the signature.</param>
        /// <param name="message">Message to encode.</param>
        /// <returns></returns>
        public static string GenerateSignature(string apiSecret, string message)
        {
            var key = Encoding.UTF8.GetBytes(apiSecret);
            string stringHash;
            using (var hmac = new HMACSHA256(key))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                stringHash = BitConverter.ToString(hash).Replace("-", "");
            }

            return stringHash;
        }

        /// <summary>
        /// Converts a DateTime to a UNIX timestamp.
        /// </summary>
        /// <param name="baseDateTime">Datetime to convert.</param>
        /// <returns>UNIX Timestamp.</returns>
        public static string GenerateTimeStamp(DateTime baseDateTime)
        {
            var dtOffset = new DateTimeOffset(baseDateTime);
            return dtOffset.ToUnixTimeMilliseconds().ToString();
        }

        /// <summary>
        /// Converts a UNIX timestamp to a DateTime.
        /// </summary>
        /// <param name="unixTimeStamp">UNIX timestamp to convert.</param>
        /// <returns>DateTime</returns>
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixTimeStamp);
            return dtDateTime;
        }

        /// <summary>
        /// Gets an HttpMethod object based on a string.
        /// </summary>
        /// <param name="method">Name of the HttpMethod to create.</param>
        /// <returns>HttpMethod object.</returns>
        public static HttpMethod CreateHttpMethod(string method)
        {
            switch (method.ToUpper())
            {
                case "DELETE":
                    return HttpMethod.Delete;
                case "POST":
                    return HttpMethod.Post;
                case "PUT":
                    return HttpMethod.Put;
                case "GET":
                    return HttpMethod.Get;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
