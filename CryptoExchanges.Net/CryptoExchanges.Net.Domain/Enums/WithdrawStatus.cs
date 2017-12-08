namespace CryptoExchanges.Net.Enums
{
    /// <summary>
    /// Represents the different statuses of a withdraw.
    /// </summary>
    public enum WithdrawStatus
    {
        EmailSent = 0,
        Cancelled = 1,
        AwaitingApproval = 2,
        Rejected = 3,
        Processing = 4,
        Failure = 5,
        Completed = 6
    }
}
