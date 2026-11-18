using Volo.Abp.Domain.Entities;

namespace WashGo.Domain.Entities.SystemConfig;

/// <summary>Mã nghiệp vụ đã cấp (kể cả của bản ghi đã xóa mềm): một mã cho một tài nguyên, không tái sử dụng.</summary>
public class BusinessCodeClaim : Entity<string>
{
    public BusinessCodeClaim() { }
    public BusinessCodeClaim(string code) : base(code) { }
    public string Prefix { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTime IssuedAt { get; set; }
}

/// <summary>Cấp mã trong transaction của thao tác tạo; gọi lại cho cùng tài nguyên trả lại mã đã cấp.</summary>
public interface IBusinessCodeAllocator
{
    /// <summary>Ném CatalogRuleException CodeSpaceExhausted khi đã hết 10.000 mã của tiền tố.</summary>
    Task<string> AllocateAsync(string prefix, Guid resourceId);
}
