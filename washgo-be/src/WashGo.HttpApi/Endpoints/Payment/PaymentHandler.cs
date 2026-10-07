using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Payment
{
    public class PaymentHandler : IEndpointBase
    {
        private const string Endpoint = "/payment";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(Endpoint)
                .WithTags("Payment - Thanh toán");

            group.MapPost("/create-url", async (CreatePaymentCommand command, IPaymentAppService service) =>
                (await service.CreatePaymentAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Tạo URL thanh toán sandbox")
                .WithOpenApi();

            group.MapPost("/callback", async (PaymentCallbackCommand command, IPaymentAppService service) =>
                (await service.HandleCallbackAsync(command)).CustomResult())
                .AllowAnonymous()
                .WithName("Payment callback sandbox")
                .WithOpenApi();
        }
    }
}
