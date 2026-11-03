using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Identity.Interfaces;

public interface IOtpCodeRepository : IRepository<OtpCode, Guid>
{
    Task<OtpCode?> GetLatestActiveForUpdateAsync(string email, OtpPurpose purpose);
}
