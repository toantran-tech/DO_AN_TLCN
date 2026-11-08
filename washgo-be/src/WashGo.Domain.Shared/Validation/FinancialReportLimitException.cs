namespace WashGo.Domain.Shared.Validation;

public sealed class FinancialReportLimitException : Exception
{
    public const int MaximumSourceRows = 25000;
    public FinancialReportLimitException() : base("Financial report source exceeds the synchronous limit; reduce the date range or select one Merchant.") { }
}
