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
