using CryptoExchanges.Net.Domain;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

    /// <summary>
    /// Class to define extension methods.
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Extension method to get a enum description.
        /// </summary>
        /// <param name="value">Enum to get the description from.</param>
        /// <returns></returns>
        public static string GetDescription(this Enum value)
        {
            return ((DescriptionAttribute)Attribute.GetCustomAttribute(
                value.GetType().GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Single(x => x.GetValue(null).Equals(value)),
                typeof(DescriptionAttribute)))?.Description ?? value.ToString();
        }

        /// <summary>
        /// Extension method to get a enum description.
        /// </summary>
        /// <param name="value">Enum to get the description from.</param>
        /// <returns></returns>
        public static IExchangeClient GetBinanceClient(this ICryptoClient value)
        {
            return value.GetExchangeClient("Binance");
        }
    }

