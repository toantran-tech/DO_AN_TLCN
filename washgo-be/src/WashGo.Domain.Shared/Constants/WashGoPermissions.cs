namespace WashGo.Domain.Shared.Constants
{
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
}
