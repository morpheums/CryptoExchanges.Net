namespace CryptoExchanges.Net.Core.Enums;

/// <summary>Specifies the execution behaviour of an order.</summary>
public enum OrderType
{
    /// <summary>Executes only at the specified price or better.</summary>
    Limit,
    /// <summary>Executes immediately at the best available price.</summary>
    Market,
    /// <summary>Market sell triggered when price drops to the stop price.</summary>
    StopLoss,
    /// <summary>Limit sell triggered when price drops to the stop price.</summary>
    StopLossLimit,
    /// <summary>Market order triggered when price rises to the target.</summary>
    TakeProfit,
    /// <summary>Limit order triggered when price rises to the target.</summary>
    TakeProfitLimit,
    /// <summary>Limit order that is rejected if it would immediately match as maker.</summary>
    LimitMaker
}
