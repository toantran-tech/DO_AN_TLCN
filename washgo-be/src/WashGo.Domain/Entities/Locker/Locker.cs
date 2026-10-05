using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Core.Domain.Shared.Enums;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Locker
{
    [SevagoTable(WashGoDbProperties.Lockers, Schema = WashGoDbProperties.Schema)]
    public class Locker : BaseEntity, IHasAuditLog
    {
        public Locker()
        {
            Boxes = new List<LockerBox>();
        }

        public Locker(Guid id) : base(id)
        {
            Boxes = new List<LockerBox>();
        }

        public Guid MerchantId { get; set; }
        public Guid? ServiceAreaId { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string Code { get; set; } = string.Empty;

        [Filterable(ColumnVariant.Text)]
        public string Name { get; set; } = string.Empty;

        [Filterable(ColumnVariant.Text)]
        public string Address { get; set; } = string.Empty;

        public string? Ward { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string District { get; set; } = string.Empty;

        [Filterable(ColumnVariant.Text)]
        public string City { get; set; } = string.Empty;

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int TotalBoxes { get; set; }
        public int AvailableBoxes { get; set; }
        public LockerType LockerType { get; set; } = LockerType.Normal;
        public string? Description { get; set; }
        public string? Images { get; set; }

        [Filterable(ColumnVariant.MultiSelect)]
        public LockerStatus Status { get; set; } = LockerStatus.Active;

        public string? RejectionReason { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public virtual ICollection<LockerBox> Boxes { get; set; }
    }

    [SevagoTable(WashGoDbProperties.LockerSlots, Schema = WashGoDbProperties.Schema)]
    public class LockerBox : BaseEntity
    {
        public LockerBox() { }
        public LockerBox(Guid id) : base(id) { }

        public Guid LockerId { get; set; }
        public int SlotNumber { get; set; }

        [NotMapped]
        public string BoxNumber
        {
            get => SlotNumber.ToString();
            set => SlotNumber = int.TryParse(value, out var number) ? number : 0;
        }

        public LockerBoxSize Size { get; set; } = LockerBoxSize.Standard;
        public LockerBoxStatus Status { get; set; } = LockerBoxStatus.Available;
        public DateTime? LastUsedAt { get; set; }

        [NotMapped]
        public string? CurrentPinCode { get; set; }
    }
}