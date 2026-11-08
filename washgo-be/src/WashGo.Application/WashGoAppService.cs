using WashGo.Core.Bases;
using WashGo.Application.Features.AuditApplication.Services;

namespace WashGo.Application
{
    public abstract class WashGoAppService : BaseResponseHandler
    {
        /// <summary>Central audit trail (see Features/AuditApplication).</summary>
        protected AuditLogWriter Audit =>
            LazyServiceProvider.LazyGetRequiredService<AuditLogWriter>();
        protected AdministrativeWriteGuard Writes =>
            LazyServiceProvider.LazyGetRequiredService<AdministrativeWriteGuard>();

        protected WashGoAppService()
        {
            ObjectMapperContext = typeof(WashGoApplicationModule);
        }
    }
}
