using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Tracing;
using Volo.Abp.Users;
using WashGo.Domain.Entities.Audit;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuditApplication.Services
{
    /// <summary>One audit event. Actor defaults to the signed-in user; pass it explicitly at login.</summary>
    public sealed record AuditEntry(string Module, string Action, string ResourceType, string? ResourceId, string? ResourceCode)
    {
        public AuditResult Result { get; init; } = AuditResult.Success;
        public string? Reason { get; init; }
        public string? ErrorCode { get; init; }
        public IEnumerable<string>? ChangedFields { get; init; }
        public object? Details { get; init; }
        public Guid? ActorId { get; init; }
        public string? ActorName { get; init; }
        public string? ActorRole { get; init; }
    }

    /// <summary>
    /// Ghi nhật ký ngay khi thao tác thành công (cùng transaction hoặc ngay sau khi transaction nghiệp vụ commit), nên
    /// thao tác bị rollback không có bản ghi thành công; thao tác bị từ chối ghi Result Failed/Denied.
    /// Chi tiết được lọc các khóa nhạy cảm (mật khẩu, OTP, token, mã mở tủ, giấy tờ tùy thân).
    /// </summary>
    public class AuditLogWriter : ITransientDependency
    {
        private static readonly string[] SensitiveKeys = ["password", "otp", "token", "secret", "pincode", "qrcode", "qrstring", "identity", "cccd", "hash"];
        private readonly IRepository<AuditLog, Guid> _repo;
        private readonly ICurrentUser _currentUser;
        private readonly ICorrelationIdProvider _correlation;

        public AuditLogWriter(IRepository<AuditLog, Guid> repo, ICurrentUser currentUser, ICorrelationIdProvider correlation)
        {
            _repo = repo;
            _currentUser = currentUser;
            _correlation = correlation;
        }

        public async Task LogAsync(AuditEntry e)
        {
            var role = e.ActorRole ?? (_currentUser.IsAuthenticated ? _currentUser.Roles.FirstOrDefault() ?? "User" : "System");
            await _repo.InsertAsync(new AuditLog(Guid.NewGuid())
            {
                OccurredAt = DateTime.UtcNow,
                ActorId = e.ActorId ?? _currentUser.Id,
                ActorName = Cut(e.ActorName ?? _currentUser.Name ?? _currentUser.UserName ?? role, 150)!,
                ActorRole = Cut(role, 20)!,
                Module = e.Module,
                Action = e.Action,
                ResourceType = e.ResourceType,
                ResourceId = e.ResourceId,
                ResourceCode = Cut(e.ResourceCode, 200),
                Result = e.Result,
                Reason = Cut(e.Reason, 1000),
                ErrorCode = Cut(e.ErrorCode, 60),
                ChangedFields = e.ChangedFields == null ? null : Cut(string.Join(",", e.ChangedFields), 1000),
                Details = Sanitize(e.Details),
                CorrelationId = Cut(_correlation.Get(), 100)
            }, autoSave: true);
        }

        private static readonly string[] IgnoredDiffKeys = ["updatedAt", "updatedBy", "revision", "generatedAt"];

        /// <summary>Top-level properties whose JSON value differs (camelCase names).</summary>
        public static List<string> Diff(object? before, object after)
        {
            var a = before == null ? new JsonObject() : JsonSerializer.SerializeToNode(before, Web) as JsonObject ?? new JsonObject();
            var b = JsonSerializer.SerializeToNode(after, Web) as JsonObject ?? new JsonObject();
            return b.Select(x => x.Key)
                .Where(k => !IgnoredDiffKeys.Contains(k) && !JsonNode.DeepEquals(a[k], b[k]))
                .ToList();
        }

        /// <summary>Only the given properties of a DTO (for before/after values of a settings change).</summary>
        public static Dictionary<string, JsonNode?>? Pick(object? dto, IEnumerable<string> keys)
        {
            if (dto == null) return null;
            var obj = JsonSerializer.SerializeToNode(dto, Web) as JsonObject;
            return obj == null ? null : keys.ToDictionary(k => k, k => obj[k]?.DeepClone());
        }

        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        private static string? Cut(string? value, int max) => value == null ? null : value.Length <= max ? value : value[..max];

        /// <summary>Serialises details and drops every key that could carry a secret, a locker code or identity data.</summary>
        public static string? Sanitize(object? details)
        {
            if (details == null) return null;
            var node = JsonSerializer.SerializeToNode(details, Web);
            Strip(node);
            var json = node?.ToJsonString();
            return json == null || json.Length <= 4000 ? json : null;
        }

        private static void Strip(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (var key in obj.Select(x => x.Key).ToList())
                {
                    if (key.Equals("pin", StringComparison.OrdinalIgnoreCase) || key.Equals("qr", StringComparison.OrdinalIgnoreCase) ||
                        SensitiveKeys.Any(k => key.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        obj.Remove(key);
                    else
                        Strip(obj[key]);
                }
            }
            else if (node is JsonArray arr)
                foreach (var item in arr)
                    Strip(item);
        }
    }
}
