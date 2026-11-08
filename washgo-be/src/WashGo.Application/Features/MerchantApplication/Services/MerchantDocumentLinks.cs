using Volo.Abp.DependencyInjection;
using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.MerchantApplication.Services;

public class MerchantDocumentLinks(PrivateDocumentStore store) : ITransientDependency
{
    public async Task<string?> ApplyAsync(Merchant merchant, CreateMerchantCommand command, Guid actor)
    {
        if ((command.BusinessLicenseDocumentId.HasValue && command.RemoveBusinessLicenseDocument) ||
            (command.ContractDocumentId.HasValue && command.RemoveContractDocument) ||
            (command.SlaDocumentId.HasValue && command.RemoveSlaDocument))
            return "Không được vừa gán vừa gỡ cùng một loại tài liệu.";
        var requested = new List<(Guid, MerchantDocumentKind)>();
        if (command.BusinessLicenseDocumentId is { } license) requested.Add((license, MerchantDocumentKind.BusinessLicense));
        if (command.ContractDocumentId is { } contract) requested.Add((contract, MerchantDocumentKind.Contract));
        if (command.SlaDocumentId is { } sla) requested.Add((sla, MerchantDocumentKind.Sla));
        var error = await store.ReferenceAsync(requested, merchant.Id, actor, true);
        if (error != null) return error;
        merchant.BusinessLicenseDocumentId = command.RemoveBusinessLicenseDocument ? null : command.BusinessLicenseDocumentId ?? merchant.BusinessLicenseDocumentId;
        merchant.ContractDocumentId = command.RemoveContractDocument ? null : command.ContractDocumentId ?? merchant.ContractDocumentId;
        merchant.SlaDocumentId = command.RemoveSlaDocument ? null : command.SlaDocumentId ?? merchant.SlaDocumentId;
        return null;
    }
}
