using System;
using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.PolicyContracts.Commands
{
    /// <summary>Customer đồng ý phiên bản điều khoản đang hiển thị.</summary>
    public class AcknowledgeTermsCommand
    {
        public string Version { get; set; } = string.Empty;
        [JsonIgnore] public string? IpAddress { get; set; }
        [JsonIgnore] public string? UserAgent { get; set; }
    }

    /// <summary>Admin phát hành phiên bản điều khoản mới; Customer phải đồng ý lại khi phiên bản có hiệu lực.</summary>
    public class PublishTermsCommand
    {
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        /// <summary>Unix giây (UTC); bỏ trống = hiệu lực ngay.</summary>
        public long? EffectiveFrom { get; set; }
    }
}
