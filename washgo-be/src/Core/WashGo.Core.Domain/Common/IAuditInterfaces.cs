using NpgsqlTypes;

namespace WashGo.Core.Domain.Common
{
    public interface ICreatable
    {
        string? CreatedBy { get; set; }
        long CreatedOn { get; set; }
    }

    public interface IModifiable
    {
        string? ModifiedBy { get; set; }
        long ModifiedOn { get; set; }
    }

    public interface IDeletable
    {
        string? DeletedBy { get; set; }
        long? DeletedOn { get; set; }
    }

    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
    }

    public interface ISearchable
    {
        NpgsqlTsVector? SearchVector { get; set; }
    }

    public interface IAuditUserSnapshot
    {
        string? Created { get; set; }
        string? Modified { get; set; }
    }

    public interface IHasAuditLog
    {
    }
}
