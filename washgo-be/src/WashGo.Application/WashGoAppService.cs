using WashGo.Core.Bases;

namespace WashGo.Application
{
    public abstract class WashGoAppService : BaseResponseHandler
    {
        /// <summary>Central audit trail (see Features/AuditApplication).</summary>
        protected WashGo.Application.Features.AuditApplication.Services.AuditLogWriter Audit =>
            LazyServiceProvider.LazyGetRequiredService<WashGo.Application.Features.AuditApplication.Services.AuditLogWriter>();

        protected WashGoAppService()
        {
            ObjectMapperContext = typeof(WashGoApplicationModule);
        }
    }
}
