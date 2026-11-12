using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Application.Services;

namespace WashGo.Application.Contracts.Features.LockerContracts.Commands
{
    public class CreateLockerCommand
    {
        public Guid? OperationId { get; set; }
        /// <summary>Merchant phụ trách locker theo thứ tự ưu tiên (phần tử đầu = ưu tiên 1). Khi sửa: null = giữ nguyên.</summary>
        public List<Guid>? MerchantIds { get; set; }
        /// <summary>Cách viết ngắn khi locker chỉ có một Merchant phụ trách (dùng khi không gửi MerchantIds).</summary>
        public Guid? MerchantId { get; set; }
        public Guid? ServiceAreaId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Ward { get; set; }
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public LockerType LockerType { get; set; } = LockerType.Normal;
        public string? Description { get; set; }
        /// <summary>Ảnh đã upload qua /files/images; khi sửa, null = giữ nguyên ảnh hiện tại.</summary>
        public List<string>? ImageUrls { get; set; }
        /// <summary>Danh sách ô cụ thể; để trống thì tạo BoxCount ô đánh số 1..N với kích thước DefaultBoxSize (chỉ khi tạo mới).</summary>
        public List<CreateLockerBoxDto> Boxes { get; set; } = [];
        public int? BoxCount { get; set; }
        public LockerBoxSize DefaultBoxSize { get; set; } = LockerBoxSize.Standard;
    }

    public class UpdateLockerCommand : CreateLockerCommand
    {
        public string? ExpectedVersion { get; set; }
        public Guid Id { get; set; }
        public LockerStatus Status { get; set; } = LockerStatus.Active;
    }

    public class CreateLockerBoxDto
    {
        public string BoxNumber { get; set; } = string.Empty;
        public LockerBoxSize Size { get; set; } = LockerBoxSize.Standard;
    }

    public class UpdateLockerBoxStatusCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public Guid LockerId { get; set; }
        public Guid BoxId { get; set; }
        public LockerBoxStatus NewStatus { get; set; }
        /// <summary>Bắt buộc: trạng thái ô lúc người dùng xem; khác hiện tại → 409 BoxStatusChanged.</summary>
        public LockerBoxStatus? ExpectedBoxStatus { get; set; }
    }
    public class SetLockerStatusCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public LockerStatus Status { get; set; }
    }
}

