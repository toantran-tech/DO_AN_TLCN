using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using WashGo.Core;
using WashGo.Domain;
using WashGo.Domain.Entities.Feedback.Interfaces;
using WashGo.Domain.Entities.Identity.Interfaces;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Notification.Interfaces;
using WashGo.Domain.Entities.Payment.Interfaces;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.Promotion.Interfaces;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
using WashGo.EntityFrameworkCore.Repositories;
using WashGo.Domain.Entities.Chatbot;
using WashGo.Domain.Entities.Chatbot.Interfaces;
using LockerEntity = global::WashGo.Domain.Entities.Locker.Locker;
using NotificationEntity = global::WashGo.Domain.Entities.Notification.Notification;
using PromotionEntity = global::WashGo.Domain.Entities.Promotion.Promotion;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Payment;
using WashOrderEntity = global::WashGo.Domain.Entities.WashOrder.WashOrder;
using WashServiceEntity = global::WashGo.Domain.Entities.WashService.WashService;

namespace WashGo.EntityFrameworkCore
{
    [DependsOn(
        typeof(WashGoDomainModule),
        typeof(WashGoCoreModule),
        typeof(AbpEntityFrameworkCorePostgreSqlModule)
    )]
    public class WashGoEntityFrameworkCoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddAbpDbContext<WashGoDbContext>(options =>
            {
                options.AddDefaultRepositories(includeAllEntities: true);
                options.AddRepository<ChatConversation, ChatConversationRepository>();
                options.AddRepository<OtpCode, OtpCodeRepository>();
                options.AddRepository<WashOrderEntity, WashOrderRepository>();
                options.AddRepository<LockerEntity, LockerRepository>();
                options.AddRepository<PaymentTransaction, PaymentTransactionRepository>();
                options.AddRepository<Shipper, ShipperRepository>();
                options.AddRepository<WashServiceEntity, WashServiceRepository>();
                options.AddRepository<PromotionEntity, PromotionRepository>();
                options.AddRepository<Incident, IncidentRepository>();
                options.AddRepository<NotificationEntity, NotificationRepository>();
            });

            context.Services.AddTransient<IWashOrderRepository, WashOrderRepository>();
            context.Services.AddTransient<IChatConversationRepository, ChatConversationRepository>();
            context.Services.AddTransient<IOtpCodeRepository, OtpCodeRepository>();
            context.Services.AddTransient<ILockerRepository, LockerRepository>();
            context.Services.AddTransient<IPaymentTransactionRepository, PaymentTransactionRepository>();
            context.Services.AddTransient<IShipperRepository, ShipperRepository>();
            context.Services.AddTransient<IWashServiceRepository, WashServiceRepository>();
            context.Services.AddTransient<IPromotionRepository, PromotionRepository>();
            context.Services.AddTransient<IIncidentRepository, IncidentRepository>();
            context.Services.AddTransient<INotificationRepository, NotificationRepository>();
        }
    }
}
