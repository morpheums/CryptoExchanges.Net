namespace CryptoExchanges.Net.Core.Models;

/// <summary>Balance of a single asset in the account.</summary>
public readonly record struct AssetBalance(
    Asset Asset,
    decimal Free,
    decimal Locked)
{
    /// <summary>Total balance (free + locked).</summary>
    public decimal Total => Free + Locked;
}
