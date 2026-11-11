namespace WashGo.Application.Contracts.Features.AuditContracts.Results;

public class AdministrativeSummaryDto
{
    public int Total { get; set; }
    public Dictionary<string, int> Counts { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
    /// <summary>Dịch vụ: số đề xuất Merchant đang chờ duyệt, cùng snapshot với các số đếm.</summary>
    public int? PendingProposals { get; set; }
}
