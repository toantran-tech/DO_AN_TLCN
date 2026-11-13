using System.Text.Json;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Application.Features.ShipperApplication.Services;
using WashGo.Application.Features.WashOrderApplication.Services;
using WashGo.Domain.Shared.Enums;

static void Check(bool passed, string error) { if (!passed) throw new InvalidOperationException(error); }
var merchant = Guid.NewGuid(); var shipper = Guid.NewGuid(); var customer = Guid.NewGuid();
const string staffSecret = "staff-private@example.com";
const string customerSecret = "customer-private@example.com";
WashOrderDetailDto Fixture() => new()
{
    OrderCode = "WG-PUBLIC", CustomerId = customer, CustomerName = customerSecret, CustomerPhoneNumber = customerSecret,
    CustomerAvatarUrl = customerSecret, CustomerNote = customerSecret, DepositPinCode = "123456", PickupPinCode = "654321",
    QrCodeString = "WASHGO:DEPOSIT:WG-PUBLIC:123456", ShipperTaskPinCode = "987654", ShipperTaskQrString = "TASK-SECRET",
    MerchantId = merchant, MerchantName = staffSecret, MerchantEmail = staffSecret, MerchantPhoneNumber = staffSecret,
    MerchantImageUrls = [staffSecret], MerchantOrderCode = staffSecret,
    ShipperId = shipper, ShipperName = staffSecret, ShipperPhone = staffSecret, ShipperEmail = staffSecret,
    ShipperAvatarUrl = staffSecret, ShipperVehiclePlate = staffSecret, ShipperNote = staffSecret,
    StorageLocation = staffSecret, StorageReference = staffSecret, FinalAmount = 125000m,
    LockerName = "Public locker", ReturnBoxNumber = "2",
    Items = [new() { ServiceName = "Laundry", Note = customerSecret }],
    Timeline = [new() { ActorRole = "Customer", ActorId = customer, ActorName = customerSecret, Note = customerSecret },
        new() { ActorRole = "Merchant", ActorId = merchant, ActorName = staffSecret, Note = customerSecret },
        new() { ActorRole = "System", ActorId = shipper, ActorName = staffSecret, Note = staffSecret }]
};
var own = OrderResponsePrivacy.Apply(Fixture(), "Customer");
var json = JsonSerializer.Serialize(own);
Check(!json.Contains(staffSecret) && !json.Contains(merchant.ToString()) && !json.Contains(shipper.ToString()), "Customer identity boundary leaked staff data.");
Check(own.CustomerId == customer && own.PickupPinCode == "654321" && own.DepositPinCode == "123456" &&
    own.FinalAmount == 125000m && own.Items[0].ServiceName == "Laundry", "Privacy removed the customer's own access codes or operational information.");
foreach (var role in new[] { "Merchant", "Shipper" })
{
    var dto = OrderResponsePrivacy.Apply(Fixture(), role);
    json = JsonSerializer.Serialize(dto);
    Check(!json.Contains(customerSecret) && !json.Contains(customer.ToString()) && !json.Contains("123456") &&
        !json.Contains("654321") && !json.Contains("987654") && !json.Contains("TASK-SECRET"), "Staff response leaked Customer identity/credentials.");
    Check(dto.FinalAmount == 125000m && dto.LockerName == "Public locker", "Staff lost operational data.");
}
var admin = OrderResponsePrivacy.Apply(Fixture(), "Admin");
Check(admin.MerchantName == staffSecret && admin.CustomerName == customerSecret && admin.Timeline[0].Note == customerSecret &&
    admin.DepositPinCode.Length == 0, "Admin lost identity or exposed locker codes in the normal order response.");
var historical = ShipperDeliveryGuard.ReadResult(JsonSerializer.Serialize(Fixture()))!;
Check(!JsonSerializer.Serialize(historical).Contains(customerSecret), "Old operation snapshot bypassed the privacy boundary.");
var stableOptions = new JsonSerializerOptions { IgnoreReadOnlyProperties = true };
var firstProjection = JsonSerializer.Serialize(own, stableOptions);
var again = OrderResponsePrivacy.Apply(own, "Customer");
Check(JsonSerializer.Serialize(again, stableOptions) == firstProjection, "Repeated projection changed a safe response.");
var notification = OrderNotificationPrivacy.Apply(new NotificationDto
{
    Type = NotificationType.MovedToStorage, LinkType = "order", Content = staffSecret,
    MessageKey = "unpaidMovedToStorage.customer", MessageArgs = new()
    { ["storageLocation"] = staffSecret, ["storageReference"] = staffSecret, ["merchantId"] = merchant.ToString(), ["orderCode"] = "WG-PUBLIC" }
});
Check(!JsonSerializer.Serialize(notification).Contains(staffSecret) && notification.MessageArgs["orderCode"] == "WG-PUBLIC" &&
    notification.MessageKey == "storage.customer.private", "Legacy storage notification leaked staff data.");
var partnerNotification = OrderNotificationPrivacy.Apply(new NotificationDto
{
    LinkType = "order", MessageArgs = new() { ["reason"] = customerSecret, ["customerName"] = customerSecret,
        ["storageLocation"] = "Internal shop address", ["orderCode"] = "WG-PUBLIC" }
}, customer: false);
Check(!JsonSerializer.Serialize(partnerNotification).Contains(customerSecret) &&
    partnerNotification.MessageArgs["storageLocation"] == "Internal shop address", "Partner notification exposed Customer free text or lost custody information.");
Console.WriteLine("PASS: Customer/Partner/Admin privacy, own locker credentials, nested notes/actors, legacy operation snapshots and storage notification projection.");
