namespace WashGo.Application.Contracts.Features.AuditContracts.Results;

public class AdministrativeSummaryDto
{
    public int Total { get; set; }
    public Dictionary<string, int> Counts { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
}
