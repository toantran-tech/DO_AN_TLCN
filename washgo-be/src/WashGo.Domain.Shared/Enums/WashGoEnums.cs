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
        Cancelled = 99
    }

    public enum LockerStatus { Active = 1, Maintenance = 2, Inactive = 3 }
    public enum LockerBoxStatus { Available = 1, Occupied = 2, Reserved = 3, Maintenance = 4 }
    public enum LockerBoxSize { Small = 1, Standard = 2, Large = 3 }
    public enum PaymentStatus { Unpaid = 1, Paid = 2, Refunded = 3 }
    public enum PaymentMethod { Cash = 1, VnPay = 2, Momo = 3, ZaloPay = 4 }
    public enum WashServiceType { StandardWash = 1, DryClean = 2, IronOnly = 3, BlanketWash = 4, ShoeCare = 5 }
    public enum AuthProvider { Local = 1, Google = 2 }
    public enum OtpPurpose { Register = 1, ForgotPassword = 2, ChangeEmail = 3 }
    public enum MerchantStatus { Active = 1, Inactive = 2, Suspended = 3 }
    public enum ShipperStatus { Available = 1, Busy = 2, Offline = 3 }
    public enum VehicleType { Motorbike = 1, Car = 2, Bicycle = 3 }
    public enum LockerType { Normal = 1, Refrigerated = 2 }
    public enum ServiceApprovalStatus { Pending = 1, Approved = 2, Rejected = 3 }
    public enum ServiceCategory { Washing = 1, DryCleaning = 2, Ironing = 3, Combo = 4 }
    public enum ServiceChangeRequestType { Create = 1, Update = 2, Delete = 3 }
    public enum OrderActorType { Customer = 1, Merchant = 2, Shipper = 3, Admin = 4, System = 5 }
}