using System.Net;
using System.Security.Cryptography;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.FileContracts.Results;
using WashGo.Application.Contracts.Features.FileContracts.Services;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Auditing;
using Volo.Abp.Validation;

namespace WashGo.Application.Features.FileApplication.Services;

[RemoteService(false)]
[DisableAuditing]
public class PrivateDocumentAppService : WashGoAppService, IPrivateDocumentAppService
{
    private readonly IRepository<PrivateDocument, Guid> _repo;
    private readonly IRepository<Merchant, Guid> _merchants;
    private readonly PrivateDocumentStore _store;
    public PrivateDocumentAppService(IRepository<PrivateDocument, Guid> repo, IRepository<Merchant, Guid> merchants, PrivateDocumentStore store)
    { _repo = repo; _merchants = merchants; _store = store; }
    private bool Admin => CurrentUser.IsInRole("Admin");
    private bool Merchant => CurrentUser.IsInRole("Merchant");
    [DisableValidation]
    public async Task<BaseResponse<PrivateDocumentDto>> UploadAsync(Stream stream, long length, string fileName, string kind, Guid? merchantId)
    {
        if (!Admin && !Merchant) return Forbidden<PrivateDocumentDto>();
        if (!Enum.TryParse<MerchantDocumentKind>(kind, true, out var type) || !Enum.IsDefined(type))
            return BadRequest<PrivateDocumentDto>("Loại tài liệu phải là BusinessLicense, Contract hoặc Sla.");
        if (!Admin && (type != MerchantDocumentKind.BusinessLicense || (merchantId.HasValue && merchantId != CurrentUser.Id)))
            return Forbidden<PrivateDocumentDto>();
        if (Merchant && !Admin) merchantId = CurrentUser.Id;
        if (merchantId.HasValue && !await _merchants.AnyAsync(x => x.Id == merchantId.Value))
            return BadRequest<PrivateDocumentDto>("Merchant không tồn tại.");
        if (length <= 0 || length > PrivateDocumentValidator.MaxBytes) return BadRequest<PrivateDocumentDto>("Tài liệu phải từ 1 byte đến 10 MB.");
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (safeName.Length is 0 or > 180 || safeName.Any(char.IsControl)) return BadRequest<PrivateDocumentDto>("Tên file không hợp lệ (tối đa 180 ký tự).");
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        using var memory = new MemoryStream(); var buffer = new byte[65536]; int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            if (memory.Length + read > PrivateDocumentValidator.MaxBytes) return BadRequest<PrivateDocumentDto>("Tài liệu vượt 10 MB.");
            await memory.WriteAsync(buffer.AsMemory(0, read));
        }
        var bytes = memory.ToArray();
        var mime = PrivateDocumentValidator.ContentType(bytes, extension);
        if (mime == null) return BadRequest<PrivateDocumentDto>("Nội dung tài liệu không phải PDF/DOC/DOCX hợp lệ hoặc không khớp phần mở rộng.");
        var now = DateTime.UtcNow;
        var file = new PrivateDocument(GuidGenerator.Create())
        {
            OwnerId = CurrentUser.Id!.Value, MerchantId = merchantId, Kind = type, FileName = safeName, Extension = extension,
            ContentType = mime, Size = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), UploadedAt = now, ExpiresAt = now.AddHours(24)
        };
        Directory.CreateDirectory(_store.Root);
        var path = _store.PathOf(file);
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _repo.InsertAsync(file, autoSave: true);
            await Audit.LogAsync(new AuditEntry("Documents", "Upload", "PrivateDocument", file.Id.ToString(), null) { Details = new { kind = type.ToString() } });
            await uow.CompleteAsync();
        }
        catch { File.Delete(path); throw; }
        return Created(PrivateDocumentStore.ToDto(file));
    }
    private async Task<PrivateDocument?> AllowedAsync(Guid id)
    {
        if (!Admin && !Merchant) return null;
        var f = await _repo.FindAsync(id);
        if (f == null || f.IsDeleted || (f.ReferencedAt == null && f.ExpiresAt <= DateTime.UtcNow)) return null;
        return Admin || f.OwnerId == CurrentUser.Id || (f.ReferencedAt.HasValue && f.MerchantId == CurrentUser.Id) ? f : null;
    }
    public async Task<BaseResponse<PrivateDocumentDto>> GetAsync(Guid id)
    {
        var f = await AllowedAsync(id);
        return f == null ? NotFound<PrivateDocumentDto>("Không tìm thấy tài liệu được phép xem.") : Success(PrivateDocumentStore.ToDto(f));
    }
    public async Task<BaseResponse<PrivateDocumentDownloadDto>> DownloadAsync(Guid id)
    {
        var f = await AllowedAsync(id);
        if (f == null || !File.Exists(_store.PathOf(f))) return NotFound<PrivateDocumentDownloadDto>("Không tìm thấy tài liệu được phép tải.");
        return Success(new PrivateDocumentDownloadDto { Bytes = await File.ReadAllBytesAsync(_store.PathOf(f)), FileName = f.FileName, ContentType = f.ContentType });
    }
    public async Task<BaseResponse<bool>> DeleteDraftAsync(Guid id)
    {
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await _store.LockAsync(id);
        var f = await AllowedAsync(id);
        if (f == null || f.OwnerId != CurrentUser.Id) return NotFound<bool>("Không tìm thấy bản nháp của bạn.");
        if (f.ReferencedAt != null) return BadRequest<bool>("Tài liệu đang hoặc đã được hồ sơ/lịch sử tham chiếu; không thể xóa.");
        File.Delete(_store.PathOf(f)); f.IsDeleted = true; await _repo.UpdateAsync(f, autoSave: true);
        await uow.CompleteAsync(); return Success(true);
    }
    public async Task<BaseResponse<int>> CleanupExpiredAsync() => Admin ? Success(await _store.CleanupExpiredAsync()) : Forbidden<int>();
    public async Task<BaseResponse<int>> CleanupMediaAsync() => Admin ? Success(await LazyServiceProvider.LazyGetRequiredService<UploadedAssetRegistry>().CleanupAsync()) : Forbidden<int>();
    private static BaseResponse<T> Forbidden<T>() => new() { Succeeded = false, StatusCode = HttpStatusCode.Forbidden, Message = "Không có quyền quản lý tài liệu này." };
}
