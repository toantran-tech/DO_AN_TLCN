using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder.Interfaces;

namespace WashGo.Application.Features.FileApplication.Services;

public class UploadedAssetRegistry(IRepository<UploadedAsset, Guid> files, IWashOrderRepository locks,
    IUnitOfWorkManager units, IAsyncQueryableExecuter queries, IConfiguration configuration) : ITransientDependency
{
    public Task RegisterAsync(Guid owner, string fileName)
    {
        var now = DateTime.UtcNow;
        return RegisterCoreAsync(new UploadedAsset(Guid.ParseExact(Path.GetFileNameWithoutExtension(fileName), "N"))
        { OwnerId = owner, Url = $"/uploads/{owner:N}/{fileName}", UploadedAt = now, ExpiresAt = now.AddHours(24) });
    }
    private async Task RegisterCoreAsync(UploadedAsset asset)
    {
        using var uow = units.Begin(requiresNew: true, isTransactional: true);
        await files.InsertAsync(asset, autoSave: true); await uow.CompleteAsync();
    }
    public async Task<int> CleanupAsync()
    {
        using var scan = units.Begin(requiresNew: true, isTransactional: false);
        var now = DateTime.UtcNow;
        var ids = await queries.ToListAsync((await files.GetQueryableAsync()).Where(x => !x.IsDeleted && x.ReferencedAt == null && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id).Select(x => x.Id).Take(100));
        var count = 0;
        foreach (var id in ids)
        {
            using var write = units.Begin(requiresNew: true, isTransactional: true);
            await locks.AcquireLockAsync($"upload-asset:{id}");
            var asset = await files.FindAsync(id);
            if (asset == null || asset.IsDeleted || asset.ReferencedAt != null || asset.ExpiresAt > DateTime.UtcNow) continue;
            var path = Path.GetFullPath(Path.Combine(UploadStorage.GetRoot(configuration), asset.Url["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar)));
            var root = UploadStorage.GetRoot(configuration).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid upload path.");
            File.Delete(path); asset.IsDeleted = true;
            await files.UpdateAsync(asset, autoSave: true); await write.CompleteAsync(); count++;
        }
        await scan.CompleteAsync(); return count;
    }
}
