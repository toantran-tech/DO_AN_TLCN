namespace WashGo.Domain.Shared.Enums
{
    public enum WashOrderStatus
    {
        Pending = 1,
        Deposited = 2,
        Collected = 3,
        Washing = 4,
        Drying = 5,
        Ironing = 6,
        Folding = 7,
        AwaitingPayment = 8,
        Paid = 9,
        ReadyToReturn = 10,
        Returned = 11,
        Completed = 12,
        MovedToStorage = 13,
        Cancelled = 99
    }

}
