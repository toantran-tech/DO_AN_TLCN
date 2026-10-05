namespace WashGo.Domain.Shared.Constants
{
    public static class WashGoDbProperties
    {
        public const string Schema = "washgo";

        public const string Orders = "orders";
        public const string OrderItems = "order_items";
        public const string OrderStatusHistory = "order_status_history";
        public const string Lockers = "lockers";
        public const string LockerSlots = "locker_slots";
        public const string Services = "services";
        public const string Roles = "roles";
        public const string Users = "users";
        public const string RefreshTokens = "refresh_tokens";
        public const string OtpCodes = "otp_codes";
        public const string Customers = "customers";
        public const string Merchants = "merchants";
        public const string Shippers = "shippers";
        public const string ServiceAreas = "service_areas";
        public const string MerchantServiceAreas = "merchant_service_areas";
        public const string MerchantImages = "merchant_images";
        public const string ServiceChangeRequests = "service_change_requests";
    }
    public static class WashGoPermissions
    {
        public const string GroupName = "WashGo";

        public static class WashOrders
        {
            public const string Default = GroupName + ".WashOrders";
            public const string Create = Default + ".Create";
            public const string Update = Default + ".Update";
            public const string Delete = Default + ".Delete";
            public const string AssignShipper = Default + ".AssignShipper";
            public const string ProcessStatus = Default + ".ProcessStatus";
        }

        public static class Lockers
        {
            public const string Default = GroupName + ".Lockers";
            public const string Manage = Default + ".Manage";
            public const string View = Default + ".View";
        }

        public static class Services
        {
            public const string Default = GroupName + ".Services";
            public const string Manage = Default + ".Manage";
            public const string View = Default + ".View";
        }
    }

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
