using WashGo.HttpApi.Endpoints.Storage;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;
using WashGo.Application.Contracts;
using WashGo.Core;
using WashGo.Core.Endpoint;
using WashGo.HttpApi.Endpoints.Auth;
using WashGo.HttpApi.Endpoints.Customer;
using WashGo.HttpApi.Endpoints.Locker;
using WashGo.HttpApi.Endpoints.Merchant;
using WashGo.HttpApi.Endpoints.OrderAssignment;
using WashGo.HttpApi.Endpoints.Policy;
using WashGo.HttpApi.Endpoints.Payment;
using WashGo.HttpApi.Endpoints.Promotion;
using WashGo.HttpApi.Endpoints.Feedback;
using WashGo.HttpApi.Endpoints.Files;
using WashGo.HttpApi.Endpoints.Notification;
using WashGo.HttpApi.Endpoints.SystemSetting;
using WashGo.HttpApi.Endpoints.Shipper;
using WashGo.HttpApi.Endpoints.Finance;
using WashGo.HttpApi.Endpoints.ServiceProposal;
using WashGo.HttpApi.Endpoints.ServiceArea;
using WashGo.HttpApi.Endpoints.WashOrder;
using WashGo.HttpApi.Endpoints.WashService;
using WashGo.HttpApi.Endpoints.Admin;
using WashGo.HttpApi.Endpoints.Audit;
using WashGo.HttpApi.Endpoints.Chatbot;
using WashGo.HttpApi.Endpoints.MerchantReport;

namespace WashGo.HttpApi
{
    [DependsOn(
        typeof(WashGoApplicationContractsModule),
        typeof(WashGoCoreModule),
        typeof(AbpAspNetCoreMvcModule)
    )]
    public class WashGoHttpApiModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddTransient<IEndpointBase, AuthHandler>();
            context.Services.AddTransient<IEndpointBase, AuditLogHandler>();
            context.Services.AddTransient<IEndpointBase, AdministrativeOperationHandler>();
            context.Services.AddTransient<IEndpointBase, AccountSecurityHandler>();
            context.Services.AddTransient<IEndpointBase, AdminProfileHandler>();
            context.Services.AddTransient<IEndpointBase, AdminDashboardHandler>();
            context.Services.AddTransient<IEndpointBase, CustomerHandler>();
            context.Services.AddTransient<IEndpointBase, ChatbotHandler>();
            context.Services.AddTransient<IEndpointBase, WashOrderHandler>();
            context.Services.AddTransient<IEndpointBase, StorageHandler>();
            context.Services.AddTransient<IEndpointBase, LockerHandler>();
            context.Services.AddTransient<IEndpointBase, WashServiceHandler>();
            context.Services.AddTransient<IEndpointBase, ServiceAreaHandler>();
            context.Services.AddTransient<IEndpointBase, MerchantHandler>();
            context.Services.AddTransient<IEndpointBase, MerchantReportHandler>();
            context.Services.AddTransient<IEndpointBase, PaymentHandler>();
            context.Services.AddTransient<IEndpointBase, PromotionHandler>();
            context.Services.AddTransient<IEndpointBase, FileHandler>();
            context.Services.AddTransient<IEndpointBase, RatingHandler>();
            context.Services.AddTransient<IEndpointBase, IncidentHandler>();
            context.Services.AddTransient<IEndpointBase, NotificationHandler>();
            context.Services.AddTransient<IEndpointBase, SystemSettingHandler>();
            context.Services.AddTransient<IEndpointBase, ShipperHandler>();
            context.Services.AddTransient<IEndpointBase, ShipperProfileHandler>();
            context.Services.AddTransient<IEndpointBase, ShipperTaskHandler>();
            context.Services.AddTransient<IEndpointBase, OrderAssignmentHandler>();
            context.Services.AddTransient<IEndpointBase, FinanceHandler>();
            context.Services.AddTransient<IEndpointBase, ServiceProposalHandler>();
            context.Services.AddTransient<IEndpointBase, MerchantProfileHandler>();
            context.Services.AddTransient<IEndpointBase, PolicyHandler>();
        }
    }
}
