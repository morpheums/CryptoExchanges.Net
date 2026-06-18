namespace CryptoExchanges.Net.Core.Enums;

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
    Expired,
    /// <summary>Order is part of an order list (e.g. OCO) awaiting activation.</summary>
    PendingNew,
    /// <summary>The exchange reported a status this SDK does not recognize.</summary>
    Unknown
}
