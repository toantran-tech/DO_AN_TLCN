using Mapster;
using WashGo.Application.Mappings;
using WashGo.Application.Contracts.Features.LockerContracts.Results;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Contracts.Features.WashServiceContracts.Commands;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Enums;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var mapper = new MapsterObjectMappingProvider(WashGoMappingConfiguration.Create());
var merchant = new Merchant(Guid.NewGuid()) { BusinessName = "Mapping fixture", Rating = 3.5m };
var merchants = mapper.Map<List<Merchant>, List<MerchantResultDto>>(new List<Merchant> { merchant });
Check(merchants.Count == 1 && merchants[0].Id == merchant.Id && merchants[0].Rating == 3.5m &&
      merchants[0].Email == string.Empty && merchants[0].ImageUrls.Count == 0, "Merchant collection/ignored fields changed.");

var locker = new Locker(Guid.NewGuid())
{
    Name = "Fixture locker",
    Boxes = new List<LockerBox>
    {
        new(Guid.NewGuid()) { SlotNumber = 1, Status = LockerBoxStatus.Available },
        new(Guid.NewGuid()) { SlotNumber = 2, Status = LockerBoxStatus.Occupied }
    }
};
var lockerDto = mapper.Map<Locker, LockerResultDto>(locker);
Check(lockerDto.Id == locker.Id && lockerDto.EmptyBoxesCount == 1 && lockerDto.Boxes.Count == 2 &&
      lockerDto.Boxes[1].BoxNumber == "2" && lockerDto.ImageUrls.Count == 0, "Locker nested mapping/count changed.");
locker.Boxes = null!;
Check(mapper.Map<Locker, LockerResultDto>(locker).Boxes.Count == 0, "Null locker boxes should map to an empty list.");

var area = mapper.Map<CreateServiceAreaCommand, ServiceArea>(new CreateServiceAreaCommand
{
    Name = "Area", Code = "MAP", Latitude = 10.7m, Description = "Original"
});
var areaId = Guid.NewGuid();
var existingArea = new ServiceArea(areaId) { Name = "Original", Latitude = 10.7m, Description = "Original" };
var updatedArea = mapper.Map(new UpdateServiceAreaCommand { Id = areaId, Name = "Updated", Latitude = null, Description = null }, existingArea);
Check(area.Name == "Area" && ReferenceEquals(updatedArea, existingArea) && existingArea.Id == areaId &&
      existingArea.Latitude == null && existingArea.Description == null, "Area update must preserve entity identity and allow clearing nulls.");
Check(mapper.Map<ServiceArea, ServiceAreaResultDto>(existingArea).Name == "Updated", "Area result mapping changed.");

var service = mapper.Map<CreateWashServiceCommand, WashService>(new CreateWashServiceCommand
{
    Name = "Wash", Code = "MAP_SVC", UnitPrice = 30000m, Description = "Original"
});
service.ApprovalStatus = ServiceApprovalStatus.Approved;
var createdOn = service.CreatedOn;
var updatedService = mapper.Map(new UpdateWashServiceCommand { Name = "Updated", UnitPrice = 45000m, Description = null, IsActive = false }, service);
var serviceDto = mapper.Map<WashService, WashServiceResultDto>(service);
Check(ReferenceEquals(updatedService, service) && serviceDto.UnitPrice == 45000m && !service.IsActive &&
      service.Description == null && service.ApprovalStatus == ServiceApprovalStatus.Approved &&
      service.CreatedOn == createdOn && serviceDto.ImageUrls.Count == 0, "Service update/result mapping changed or overwrote server-owned fields.");

var actorId = Guid.NewGuid();
var order = new WashOrder
{
    OrderCode = "MAP_ORDER", Status = WashOrderStatus.Collected, TotalAmount = 100000m,
    FinalAmount = 115000m, ActualWeightKg = 6m, IncludedWeightKg = 5m,
    DepositPinCode = "123456", PickupPinCode = "654321",
    Items = new List<WashOrderItem> { new() { ServiceId = Guid.NewGuid(), ServiceName = "Wash", Quantity = 1, SubTotal = 100000m } },
    Timeline = new List<WashOrderTimeline> { new() { Status = WashOrderStatus.Collected, ActorId = actorId, ActorRole = "Shipper", Timestamp = 123 } }
};
var summary = mapper.Map<WashOrder, WashOrderResultDto>(order);
var detail = mapper.Map<WashOrder, WashOrderDetailDto>(order);
Check(summary.OrderCode == order.OrderCode && detail.FinalAmount == 115000m && detail.ExcessWeightKg == 1m &&
      detail.Items.Single().ServiceName == "Wash" && detail.Items[0].ServiceImageUrls.Count == 0 &&
      detail.Timeline.Single().ActorId == actorId && detail.DepositPinCode == "123456" &&
      detail.Services.Count == 0 && detail.MerchantPhoneNumber == null && detail.PredictedFinishAt == null,
      "Order detail/base inheritance, nested data, computed or ignored enrichment fields changed.");
order.Items = null!;
order.Timeline = null!;
detail = mapper.Map<WashOrder, WashOrderDetailDto>(order);
Check(detail.Items.Count == 0 && detail.Timeline.Count == 0, "Null order collections should map to empty lists.");

var refused = false;
try { mapper.Map<UnregisteredSource, UnregisteredDestination>(new UnregisteredSource()); }
catch (Exception ex) when (ex is CompileException or InvalidOperationException) { refused = true; }
Check(refused, "An unregistered source/destination pair must not silently map.");
Console.WriteLine("PASS: all five mapping modules, collections/nesting, DTO inheritance, null clearing, in-place updates, ignored enrichment and rejection of unregistered mapping pairs.");

public sealed class UnregisteredSource { public string Name { get; set; } = "Unregistered"; }
public sealed class UnregisteredDestination { public string Name { get; set; } = string.Empty; }
