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
                options.AddRepository<WashGo.Domain.Entities.Identity.OtpCode, OtpCodeRepository>();
                options.AddRepository<WashGo.Domain.Entities.WashOrder.WashOrder, WashOrderRepository>();
                options.AddRepository<WashGo.Domain.Entities.Locker.Locker, LockerRepository>();
                options.AddRepository<WashGo.Domain.Entities.Payment.PaymentTransaction, PaymentTransactionRepository>();
                options.AddRepository<WashGo.Domain.Entities.Partner.Shipper, ShipperRepository>();
                options.AddRepository<WashGo.Domain.Entities.WashService.WashService, WashServiceRepository>();
                options.AddRepository<WashGo.Domain.Entities.Promotion.Promotion, PromotionRepository>();
                options.AddRepository<WashGo.Domain.Entities.Feedback.Incident, IncidentRepository>();
                options.AddRepository<WashGo.Domain.Entities.Notification.Notification, NotificationRepository>();
            });

            context.Services.AddTransient<IWashOrderRepository, WashOrderRepository>();
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
