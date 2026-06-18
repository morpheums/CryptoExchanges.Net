namespace CryptoExchanges.Net.Core.Enums;

/// <summary>Controls how long an order remains active once placed.</summary>
public enum TimeInForce
{
    /// <summary>Good-Till-Canceled. Order remains active until filled or canceled.</summary>
    Gtc,
    /// <summary>Immediate-or-Cancel. Fill any amount immediately; cancel the rest.</summary>
    Ioc,
    /// <summary>Fill-or-Kill. Must fill entirely immediately or cancel completely.</summary>
    Fok
}
