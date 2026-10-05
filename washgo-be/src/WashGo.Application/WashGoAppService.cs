using WashGo.Core.Bases;

namespace WashGo.Application
{
    public abstract class WashGoAppService : BaseResponseHandler
    {
        protected WashGoAppService()
        {
            ObjectMapperContext = typeof(WashGoApplicationModule);
        }
    }
}
