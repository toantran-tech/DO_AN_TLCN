using System;

namespace WashGo.Application.Contracts.Features.PolicyContracts.Results
{
    public class TermsPolicyDto
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Quy định đang áp dụng, lấy từ cài đặt hệ thống (UC 3.9) tại thời điểm gọi.</summary>
    public class PolicyRulesDto
    {
        public int DepositWindowMinutes { get; set; }
        public int CancelLatePendingPercent { get; set; }
        public int CancelDepositedPercent { get; set; }
        public int CancelCollectedPercent { get; set; }
        public int FreePickupHours { get; set; }
        public decimal OverdueFeeDay1 { get; set; }
        public decimal OverdueFeeDay2 { get; set; }
        public decimal OverdueFeeDay3 { get; set; }
        public decimal StorageFeePerDay { get; set; }
        public int StorageTransferAfterHours { get; set; }
        public int UnpaidStorageAfterDays { get; set; }
        public int CompensationLightPercent { get; set; }
        public int CompensationMediumPercent { get; set; }
        public int CompensationSeverePercent { get; set; }
        public int CompensationLostPercent { get; set; }
        public DateTime? SettingsUpdatedAt { get; set; }
    }

    public class CurrentPolicyDto
    {
        public TermsPolicyDto Terms { get; set; } = new();
        public PolicyRulesDto Rules { get; set; } = new();
        public DateTime ServerNow { get; set; }
    }

    /// <summary>Điều khoản + trạng thái đồng ý của Customer đang đăng nhập.</summary>
    public class CustomerPolicyDto : CurrentPolicyDto
    {
        public bool Accepted { get; set; }
        public DateTime? AcceptedAt { get; set; }
        /// <summary>Phiên bản Customer đã đồng ý gần nhất (có thể cũ hơn phiên bản hiện hành).</summary>
        public string? AcceptedVersion { get; set; }
        public bool RequiresAcceptance => !Accepted;
    }
}
