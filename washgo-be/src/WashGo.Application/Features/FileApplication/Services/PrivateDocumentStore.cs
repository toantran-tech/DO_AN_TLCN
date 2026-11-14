using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.FileContracts.Results;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.FileApplication.Services;

public class PrivateDocumentStore : ITransientDependency
{
    private readonly IRepository<PrivateDocument, Guid> _repo;
    private readonly IWashOrderRepository _locks;
    private readonly IAsyncQueryableExecuter _queries;
    private readonly IUnitOfWorkManager _uow;
    private readonly ObjectStorage _objects;
    public PrivateDocumentStore(IRepository<PrivateDocument, Guid> repo, IWashOrderRepository locks,
        IAsyncQueryableExecuter queries, IUnitOfWorkManager uow, ObjectStorage objects)
    {
        _repo = repo; _locks = locks; _queries = queries; _uow = uow;
        _objects = objects;
    }
    private static string Key(PrivateDocument file) => file.Id.ToString("N") + file.Extension;
    public Task<bool> ExistsAsync(PrivateDocument file) => _objects.ExistsAsync(Key(file), documents: true);
    public Task PutAsync(PrivateDocument file, Stream content) => _objects.PutAsync(Key(file), content, file.ContentType, documents: true);
    public Task<byte[]?> ReadAsync(PrivateDocument file) => _objects.ReadAsync(Key(file), PrivateDocumentValidator.MaxBytes, documents: true);
    public Task DeleteAsync(PrivateDocument file) => _objects.DeleteAsync(Key(file), documents: true);
    public Task LockAsync(Guid id) => _locks.AcquireLockAsync($"private-document:{id}");
    public static PrivateDocumentDto ToDto(PrivateDocument f) => new()
    {
        FileId = f.Id, OriginalFileName = f.FileName, ContentType = f.ContentType, Size = f.Size, Kind = f.Kind,
        UploadedAt = f.UploadedAt, ExpiresAt = f.ReferencedAt == null ? f.ExpiresAt : null
    };
    public async Task<Dictionary<Guid, PrivateDocumentDto>> MetadataAsync(IEnumerable<Guid?> ids)
    {
        var keys = ids.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (keys.Length == 0) return [];
        return (await _repo.GetListAsync(x => keys.Contains(x.Id) && !x.IsDeleted)).ToDictionary(x => x.Id, ToDto);
    }

    /// <summary>Call inside the profile/request transaction, after its Merchant lock. Referenced files never expire.</summary>
    public async Task<string?> ReferenceAsync(IEnumerable<(Guid Id, MerchantDocumentKind Kind)> requested, Guid merchantId, Guid actorId, bool admin)
    {
        var files = requested.Distinct().OrderBy(x => x.Id).ToList();
        foreach (var (id, _) in files) await LockAsync(id);
        foreach (var (id, kind) in files)
        {
            var f = await _repo.FindAsync(id);
            if (f == null || f.IsDeleted || f.Kind != kind || !await ExistsAsync(f) ||
                (f.ReferencedAt == null && f.ExpiresAt <= DateTime.UtcNow) ||
                (f.MerchantId.HasValue && f.MerchantId != merchantId) ||
                (f.OwnerId != actorId && !(admin && f.ReferencedAt.HasValue && f.MerchantId == merchantId)))
                return "Tài liệu không hợp lệ, đã hết hạn hoặc không thuộc quyền gán cho Merchant này.";
            if (!admin && (kind != MerchantDocumentKind.BusinessLicense || f.OwnerId != actorId || merchantId != actorId))
                return "Merchant chỉ được đề xuất giấy phép do chính mình upload.";
            f.MerchantId = merchantId;
            f.ReferencedAt ??= DateTime.UtcNow;
            await _repo.UpdateAsync(f, autoSave: true);
        }
        return null;
    }

    public async Task<int> CleanupExpiredAsync()
    {
        using var scan = _uow.Begin(requiresNew: true, isTransactional: false);
        var now = DateTime.UtcNow;
        var ids = await _queries.ToListAsync((await _repo.GetQueryableAsync()).Where(x => !x.IsDeleted && x.ReferencedAt == null && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id).Select(x => x.Id).Take(100));
        var count = 0;
        foreach (var id in ids)
        {
            using var write = _uow.Begin(requiresNew: true, isTransactional: true);
            await LockAsync(id);
            var file = await _repo.FindAsync(id);
            if (file == null || file.IsDeleted || file.ReferencedAt != null || file.ExpiresAt > DateTime.UtcNow) continue;
            await DeleteAsync(file);
            file.IsDeleted = true;
            await _repo.UpdateAsync(file, autoSave: true);
            await write.CompleteAsync(); count++;
        }
        await scan.CompleteAsync();
        return count;
    }
}
