using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Identity.Interfaces;
using WashGo.Domain.Shared.Enums;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

public class OtpCodeRepository : MesRepository<WashGoDbContext, OtpCode, Guid>, IOtpCodeRepository
{
    public OtpCodeRepository(IDbContextProvider<WashGoDbContext> dbContextProvider, IObjectMapper objectMapper)
        : base(dbContextProvider, objectMapper) { }

    public async Task<OtpCode?> GetLatestActiveForUpdateAsync(string email, OtpPurpose purpose)
    {
        var db = await GetDbContextAsync();
        var now = DateTime.UtcNow;
        var rows = await db.OtpCodes.FromSqlInterpolated(
            $"SELECT * FROM washgo.otp_codes WHERE lower(email) = {email} AND purpose = {(int)purpose} AND is_used = false AND expiry_date > {now} ORDER BY created_at DESC LIMIT 1 FOR UPDATE")
            .ToListAsync();
        return rows.FirstOrDefault();
    }
}
