namespace CryptoExchanges.Net.Core.Enums;

/// <summary>Specifies whether an order is a buy or sell.</summary>
public enum OrderSide
{
    /// <summary>Buy order.</summary>
    Buy,
    /// <summary>Sell order.</summary>
    Sell
}

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

/// <summary>Represents the lifecycle state of an order.</summary>
public enum OrderStatus
{
    /// <summary>Order has been accepted but not yet filled.</summary>
    New,
    /// <summary>Order has been partially filled and remains open.</summary>
    PartiallyFilled,
    /// <summary>Order has been completely filled.</summary>
    Filled,
    /// <summary>Order was canceled by the user.</summary>
    Canceled,
    /// <summary>Cancellation has been requested but not yet confirmed.</summary>
    PendingCancel,
    /// <summary>Order was rejected by the exchange.</summary>
    Rejected,
    /// <summary>Order expired without being filled.</summary>
    Expired
}

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

/// <summary>Represents the time interval for a candlestick (kline) bar.</summary>
public enum KlineInterval
{
    /// <summary>One minute.</summary>
    OneMinute,
    /// <summary>Three minutes.</summary>
    ThreeMinutes,
    /// <summary>Five minutes.</summary>
    FiveMinutes,
    /// <summary>Fifteen minutes.</summary>
    FifteenMinutes,
    /// <summary>Thirty minutes.</summary>
    ThirtyMinutes,
    /// <summary>One hour.</summary>
    OneHour,
    /// <summary>Two hours.</summary>
    TwoHours,
    /// <summary>Four hours.</summary>
    FourHours,
    /// <summary>Six hours.</summary>
    SixHours,
    /// <summary>Eight hours.</summary>
    EightHours,
    /// <summary>Twelve hours.</summary>
    TwelveHours,
    /// <summary>One day.</summary>
    OneDay,
    /// <summary>Three days.</summary>
    ThreeDays,
    /// <summary>One week.</summary>
    OneWeek,
    /// <summary>One month.</summary>
    OneMonth
}

/// <summary>Identifies a supported cryptocurrency exchange.</summary>
public enum ExchangeId
{
    /// <summary>Binance.</summary>
    Binance,
    /// <summary>Coinbase.</summary>
    Coinbase,
    /// <summary>Bybit.</summary>
    Bybit,
    /// <summary>Kraken.</summary>
    Kraken,
    /// <summary>OKX.</summary>
    Okx,
    /// <summary>KuCoin.</summary>
    Kucoin
}
