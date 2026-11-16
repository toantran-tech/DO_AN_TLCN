using System.Globalization;
using Mapster;
using WashGo.Application.Mappings;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Features.WashOrderApplication.Services;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Validation;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
Check(ServiceCategoryRules.Key("  Giặt   chăn  ") == ServiceCategoryRules.Key("giặt chăn"), "Category case/space normalization");
Check(ServiceCategoryRules.Key("a\u0301") == ServiceCategoryRules.Key("á"), "Category Unicode normalization");
Check(ServiceCategoryRules.LegacyId(4).ToString() == "ca000000-0000-0000-0000-000000000004", "Blanket mapped to Combo");
Check(ServiceCategoryRules.Validate("INVALID CODE", "Valid", null, 0) != null, "Invalid code accepted");
Check(ServiceCategoryRules.Validate("VALID_CODE", "<script>", null, 0) != null, "HTML accepted");
Check(ServiceCategoryRules.Validate("VALID_CODE", "Category six", null, 100000) == null, "New category bounded by enum");
var culture = CultureInfo.CurrentCulture;
try {
    CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
    Check(StorageReferenceRules.Format(new DateTime(2026,11,16,17,0,0,DateTimeKind.Utc), 42) == "LK202611170000000042", "UTC+7/date/leading zeros depend on client culture");
} finally { CultureInfo.CurrentCulture = culture; }
var config = WashGoMappingConfiguration.Create();
var row = new WashOrderTimeline(Guid.NewGuid()) { WashOrderId = Guid.NewGuid(), EventType = "MerchantAssigned", Sequence = 4,
    FromStatus = WashOrderStatus.Deposited, Status = WashOrderStatus.Deposited, MerchantId = Guid.NewGuid(), MerchantName = "Original shop", AssignmentMode = "Automatic" };
var dto = row.Adapt<WashOrderTimelineDto>(config);
Check(dto.FromStatus == dto.Status && dto.EventType == "MerchantAssigned" && dto.Sequence == 4 && dto.MerchantName == "Original shop", "Structured event mapping lost metadata");
Check(OrderEventLabels.Title("MerchantAssigned", row.Status) != OrderEventLabels.Title("Deposited", row.Status), "Assignment rendered as deposit");
Console.WriteLine("PASS: normalized dynamic categories, deterministic legacy mapping, culture-independent LK code and structured timeline mapping.");
