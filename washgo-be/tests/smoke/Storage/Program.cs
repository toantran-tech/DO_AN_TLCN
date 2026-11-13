using Microsoft.Extensions.Configuration;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Application.Features.StorageApplication.Services;
using WashGo.Domain.Entities.SystemConfig;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

static void Check(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
var now = new DateTime(2026,11,14,0,0,0,DateTimeKind.Utc);
var rules = new SystemSetting { OverdueFeeDay1 = 10000, OverdueFeeDay2 = 20000, OverdueFeeDay3 = 50000, StorageFeePerDay = 100000 };
var order = new WashOrder { StorageWorkflowVersion = 1, Status = WashOrderStatus.ReadyToReturn, PickupDeadline = now.AddHours(-73) };
Check(OverdueFeeCalculator.GetTotalStorageDue(order,now,rules) == 180000, "Locker fee bands changed.");
order.AccumulatedStorageFee=180000; order.Status=WashOrderStatus.TransferringToStorage;
Check(OverdueFeeCalculator.GetTotalStorageDue(order,now.AddDays(2),rules)==180000, "Inbound transit accrued storage fee.");
order.Status=WashOrderStatus.MovedToStorage; order.MovedToStorageAt=now;
foreach(var (elapsed,fee) in new[]{(TimeSpan.Zero,0m),(TimeSpan.FromTicks(1),100000m),(TimeSpan.FromHours(24),100000m),(TimeSpan.FromHours(24).Add(TimeSpan.FromTicks(1)),200000m)})
    Check(OverdueFeeCalculator.GetTotalStorageDue(order,now+elapsed,rules)==180000+fee, "Started-day rounding failed.");
order.AccumulatedStorageFee=280000; order.Status=WashOrderStatus.ReturningFromStorage;
Check(OverdueFeeCalculator.GetTotalStorageDue(order,now.AddDays(3),rules)==280000, "Outbound transit accrued fees.");
order.Status=WashOrderStatus.ReadyToReturn; order.PickupDeadline=now.AddHours(24); order.MovedToStorageAt=null;
Check(OverdueFeeCalculator.GetTotalStorageDue(order,now.AddHours(24),rules)==280000 &&
    OverdueFeeCalculator.GetTotalStorageDue(order,now.AddHours(24).AddTicks(1),rules)==290000, "Redelivery erased past fees or the new free period.");
order.AccumulatedStorageFee=560000; order.Status=WashOrderStatus.Completed; order.OverdueFeeTotal=560000;
Check(OverdueFeeCalculator.GetTotalStorageDue(order,now.AddYears(1),rules)==560000, "Completed order continued billing.");
var legacy = new WashOrder { StorageWorkflowVersion=0, Status=WashOrderStatus.MovedToStorage, MovedToStorageAt=now, LockerOverdueFeeAtStorageTransfer=80000 };
Check(OverdueFeeCalculator.GetTotalStorageDue(legacy,now.AddHours(1),rules)==180000, "Legacy fee semantics changed before adoption.");
foreach(var state in new[]{WashOrderStatus.ReadyToReturn,WashOrderStatus.TransferringToStorage})
    Check(DeliveryTaskRules.IsActive(true,state,DeliveryTaskType.StorageTransfer,default), "Inbound busy predicate missing.");
foreach(var state in new[]{WashOrderStatus.MovedToStorage,WashOrderStatus.ReturningFromStorage})
    Check(DeliveryTaskRules.IsActive(true,state,DeliveryTaskType.StorageReturn,default), "Outbound busy predicate missing.");
Check(!DeliveryTaskRules.IsActive(true,WashOrderStatus.Completed,DeliveryTaskType.StorageReturn,default), "Completed task kept Shipper busy.");
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:SecretKey"]="CI-only-storage-quote-signing-key-not-for-users" }).Build();
var codec=new StorageQuoteCodec(configuration); var owner=Guid.NewGuid();var id=Guid.NewGuid();
var token=codec.Write(new(id,owner,Guid.NewGuid(),null,280000,new DateTimeOffset(now.AddMinutes(5)).ToUnixTimeSeconds()));
Check(codec.Read(token,id,owner,now)?.FeeTotal==280000, "Quote roundtrip failed.");
Check(codec.Read(token+"0",id,owner,now)==null && codec.Read(token,id,Guid.NewGuid(),now)==null &&
    codec.Read(token,Guid.NewGuid(),owner,now)==null && codec.Read(token,id,owner,now.AddMinutes(5))==null, "Quote tampering/owner/expiry accepted.");
Console.WriteLine("PASS: cumulative multi-cycle fees, transit freeze, started-day boundaries, free period, completion freeze, legacy, Shipper busy and signed quote ownership/expiry.");
