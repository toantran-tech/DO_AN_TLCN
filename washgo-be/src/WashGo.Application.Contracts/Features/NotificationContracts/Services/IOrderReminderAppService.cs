using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Services
{
    public interface IOrderReminderAppService : IApplicationService
    {
        /// <summary>Quét đơn và gửi các lời nhắc đến hạn; gọi định kỳ bởi worker hoặc thủ công bởi Admin.</summary>
        Task<BaseResponse<ReminderRunResultDto>> RunAsync();
    }
}
