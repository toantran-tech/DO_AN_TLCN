namespace WashGo.Domain.Shared.Enums
{
    public enum IncidentStatus
    {
        Pending = 1,
        Resolved = 2,
        Rejected = 3,
        /// <summary>Khách tự rút khiếu nại khi còn chờ xử lý.</summary>
        Withdrawn = 4
    }
}
