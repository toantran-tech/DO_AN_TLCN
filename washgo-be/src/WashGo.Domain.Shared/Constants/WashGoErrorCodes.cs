namespace WashGo.Domain.Shared.Constants
{

    public static class WashGoErrorCodes
    {
        public const string OrderNotFound = "WASHGO:001";
        public const string OrderInvalidStatusTransition = "WASHGO:002";
        public const string LockerBoxNotAvailable = "WASHGO:003";
        public const string LockerNotFound = "WASHGO:004";
        public const string ServiceNotFound = "WASHGO:005";
        public const string InvalidOtpOrQrCode = "WASHGO:006";
        public const string LockerBoxOccupied = "WASHGO:007";
    }
}
