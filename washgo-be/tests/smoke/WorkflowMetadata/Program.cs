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
Check(ServiceCategoryRules.Validate("<script>", null, 0) != null, "HTML accepted");
Check(ServiceCategoryRules.Validate("Category six", null, 100000) == null, "New category bounded by enum");
Check(ServiceCategoryRules.Validate("Category six", null, 100001) != null, "Sort order unbounded");
var culture = CultureInfo.CurrentCulture;
try {
    CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
    Check(StorageReferenceRules.Format(42) == "ST0042", "Storage reference leading zeros depend on client culture");
    Check(BusinessCodeRules.Format(BusinessCodeRules.Category, 7) == "DM0007", "Business code leading zeros depend on client culture");
} finally { CultureInfo.CurrentCulture = culture; }
Check(StorageReferenceRules.IsIssuedFormat("ST0042") && StorageReferenceRules.IsIssuedFormat("LK202611170000000042") && !StorageReferenceRules.IsIssuedFormat("ST42"), "Issued storage references (new ST and legacy LK) not recognized");
Check(BusinessCodeRules.AcceptsOnUpdate(null, "SVC_WASH") && BusinessCodeRules.AcceptsOnUpdate("svc_wash", "SVC_WASH") && !BusinessCodeRules.AcceptsOnUpdate("SV0001", "SVC_WASH"), "Stored code is not immutable");
var config = WashGoMappingConfiguration.Create();
var row = new WashOrderTimeline(Guid.NewGuid()) { WashOrderId = Guid.NewGuid(), EventType = "MerchantAssigned", Sequence = 4,
    FromStatus = WashOrderStatus.Deposited, Status = WashOrderStatus.Deposited, MerchantId = Guid.NewGuid(), MerchantName = "Original shop", AssignmentMode = "Automatic" };
var dto = row.Adapt<WashOrderTimelineDto>(config);
Check(dto.FromStatus == dto.Status && dto.EventType == "MerchantAssigned" && dto.Sequence == 4 && dto.MerchantName == "Original shop", "Structured event mapping lost metadata");
Check(OrderEventLabels.Title("MerchantAssigned", row.Status) != OrderEventLabels.Title("Deposited", row.Status), "Assignment rendered as deposit");
Console.WriteLine("PASS: normalized dynamic categories, deterministic legacy mapping, culture-independent ST/DM codes, legacy LK references, immutable codes and structured timeline mapping.");
