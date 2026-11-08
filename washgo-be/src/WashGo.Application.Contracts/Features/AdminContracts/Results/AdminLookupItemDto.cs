using System;

namespace WashGo.Application.Contracts.Features.AdminContracts.Results
{
    public class AdminLookupItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int Status { get; set; }
        /// <summary>Locker: số ô trống / tổng ô.</summary>
        public int? AvailableBoxes { get; set; }
        public int? TotalBoxes { get; set; }
    }
}
