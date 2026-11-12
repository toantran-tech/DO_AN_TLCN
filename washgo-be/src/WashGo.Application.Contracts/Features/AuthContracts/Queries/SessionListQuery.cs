namespace WashGo.Application.Contracts.Features.AuthContracts.Queries
{
    /// <summary>page từ 1, take 1-100 (mặc định 20); mới hoạt động nhất trước.</summary>
    public class SessionListQuery
    {
        public int? Page { get; set; }
        public int? Take { get; set; }
    }
}
