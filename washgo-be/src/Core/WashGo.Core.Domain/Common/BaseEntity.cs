using System;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;
using Volo.Abp.Domain.Entities;

namespace WashGo.Core.Domain.Common
{
    public abstract class BaseEntity : Entity<Guid>, IModifiable, ICreatable, IDeletable, ISearchable, ISoftDelete, IAuditUserSnapshot
    {
        protected BaseEntity()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CreatedOn = now;
            ModifiedOn = now;
        }

        protected BaseEntity(Guid id) : base(id)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CreatedOn = now;
            ModifiedOn = now;
        }

        public string? CreatedBy { get; set; }
        public long CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public long ModifiedOn { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public long? DeletedOn { get; set; }

        public NpgsqlTsVector? SearchVector { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Created { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Modified { get; set; }

        public void AssignIdIfEmptyForBulkInsert()
        {
            if (Id == Guid.Empty) Id = Guid.NewGuid();
        }
    }
}
