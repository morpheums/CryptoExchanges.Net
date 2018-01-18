using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    /// Validates if a string is a valid int.
    /// </summary>
    /// <param name="value">String to validate.</param>
    /// <returns></returns>
    public static bool IsValidInt(this string value)
    {
        var result = int.TryParse(value, out int parsedValue);
        return result;
    }

    /// <summary>
    /// Validates if string is a valid JSON
    /// </summary>
    /// <param name="stringValue">String to validate</param>
    /// <returns></returns>
    public static bool IsValidJson(this string stringValue)
    {
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        var value = stringValue.Trim();

        if ((value.StartsWith("{") && value.EndsWith("}")) || //For object
            (value.StartsWith("[") && value.EndsWith("]"))) //For array
        {
            try
            {
                var obj = JToken.Parse(value);
                return true;
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        return false;
    }
}

