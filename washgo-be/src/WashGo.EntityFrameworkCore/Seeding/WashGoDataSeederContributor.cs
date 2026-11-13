using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.Policy;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Enums;
using PasswordHasher = global::BCrypt.Net.BCrypt;

namespace WashGo.EntityFrameworkCore.Seeding
{
    public class WashGoDataSeederContributor : IDataSeedContributor, ITransientDependency
    {
        private readonly IRepository<Role, Guid> _roleRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Customer, Guid> _customerRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<Shipper, Guid> _shipperRepo;
        private readonly IRepository<ServiceArea, Guid> _serviceAreaRepo;
        private readonly IRepository<MerchantServiceArea, Guid> _merchantServiceAreaRepo;
        private readonly IRepository<Locker, Guid> _lockerRepo;
        private readonly IRepository<LockerMerchant, Guid> _lockerMerchantRepo;
        private readonly IRepository<WashService, Guid> _washServiceRepo;
        private readonly IRepository<TermsPolicy, Guid> _termsRepo;

        public WashGoDataSeederContributor(
            IRepository<Role, Guid> roleRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Customer, Guid> customerRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<Shipper, Guid> shipperRepo,
            IRepository<ServiceArea, Guid> serviceAreaRepo,
            IRepository<MerchantServiceArea, Guid> merchantServiceAreaRepo,
            IRepository<Locker, Guid> lockerRepo,
            IRepository<LockerMerchant, Guid> lockerMerchantRepo,
            IRepository<WashService, Guid> washServiceRepo,
            IRepository<TermsPolicy, Guid> termsRepo)
        {
            _roleRepo = roleRepo;
            _userRepo = userRepo;
            _customerRepo = customerRepo;
            _merchantRepo = merchantRepo;
            _shipperRepo = shipperRepo;
            _serviceAreaRepo = serviceAreaRepo;
            _merchantServiceAreaRepo = merchantServiceAreaRepo;
            _lockerRepo = lockerRepo;
            _lockerMerchantRepo = lockerMerchantRepo;
            _washServiceRepo = washServiceRepo;
            _termsRepo = termsRepo;
        }

        public async Task SeedAsync(DataSeedContext context)
        {
            // Fixed GUIDs for predictable testing
            var adminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var customerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var merchantRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var shipperRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

            // 1. Roles
            var adminRole = await SeedRoleAsync(adminRoleId, "Admin", "Quản trị viên hệ thống");
            var customerRole = await SeedRoleAsync(customerRoleId, "Customer", "Khách hàng sử dụng dịch vụ");
            var merchantRole = await SeedRoleAsync(merchantRoleId, "Merchant", "Đối tác giặt sấy WashGo");
            var shipperRole = await SeedRoleAsync(shipperRoleId, "Shipper", "Nhân viên giao nhận");

            // 2. Users
            var adminUserId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
            var customerUserId = Guid.Parse("c2222222-2222-2222-2222-222222222222");
            var merchantUserId = Guid.Parse("d3333333-3333-3333-3333-333333333333");
            var shipperUserId = Guid.Parse("e4444444-4444-4444-4444-444444444444");

            var adminPassHash = PasswordHasher.HashPassword("Admin@123");
            var customerPassHash = PasswordHasher.HashPassword("Customer@123");
            var merchantPassHash = PasswordHasher.HashPassword("Merchant@123");
            var shipperPassHash = PasswordHasher.HashPassword("Shipper@123");

            await SeedUserAsync(adminUserId, adminRole.Id, "admin@washgo.vn", adminPassHash, "Admin WashGo", "0901000001");
            await SeedUserAsync(customerUserId, customerRole.Id, "customer@washgo.vn", customerPassHash, "Nguyễn Văn Khách", "0902000002");
            await SeedUserAsync(merchantUserId, merchantRole.Id, "merchant@washgo.vn", merchantPassHash, "Cửa Hàng Giặt Sấy WashGo Q1", "0903000003");
            await SeedUserAsync(shipperUserId, shipperRole.Id, "shipper@washgo.vn", shipperPassHash, "Trần Văn Shipper", "0904000004");

            // 3. Profiles
            await SeedCustomerProfileAsync(customerUserId, "123 Nguyễn Huệ", "TP. Hồ Chí Minh");
            await SeedMerchantProfileAsync(merchantUserId, "Giặt Sấy WashGo Q1", "45 Lê Thánh Tôn", "Quận 1", "TP. Hồ Chí Minh", "0903000003");
            await SeedShipperProfileAsync(shipperUserId, merchantUserId, "59-X1 12345");

            // 4. Service Area
            var serviceAreaId = Guid.Parse("e1111111-1111-1111-1111-111111111111");
            await SeedServiceAreaAsync(serviceAreaId, "SA_Q1", "Khu vực Quận 1", "Quận 1", "TP. Hồ Chí Minh", 10.7769m, 106.7009m, 5);

            // 5. Merchant Service Area
            var msaId = Guid.Parse("f1111111-1111-1111-1111-111111111111");
            await SeedMerchantServiceAreaAsync(msaId, merchantUserId, serviceAreaId);

            // 6. Locker & Slots
            var lockerId = Guid.Parse("a1111111-aaaa-1111-aaaa-111111111111");
            await SeedLockerAsync(lockerId, merchantUserId, serviceAreaId, "LK_Q1_001", "Trạm Locker Bến Thành", "123 Lê Lợi", "Quận 1", "TP. Hồ Chí Minh");
            await SeedReturnLockerAsync(merchantUserId, serviceAreaId);

            // 7. Wash Services
            var service1Id = Guid.Parse("b1111111-bbbb-1111-bbbb-111111111111");
            var service2Id = Guid.Parse("b2222222-bbbb-2222-bbbb-222222222222");
            var service3Id = Guid.Parse("b3333333-bbbb-3333-bbbb-333333333333");

            await SeedWashServiceAsync(service1Id, merchantUserId, "SVC_WASH_DRY", "Giặt sấy sấy khô cao cấp", 30000, 30000, null, WashServiceType.StandardWash, "kg");
            await SeedWashServiceAsync(service2Id, merchantUserId, "SVC_DRY_CLEAN", "Giặt hấp giặt khô vest/đầm", 80000, null, 80000, WashServiceType.DryClean, "cái");
            await SeedWashServiceAsync(service3Id, merchantUserId, "SVC_SHOES", "Vệ sinh giày thể thao", 50000, null, 50000, WashServiceType.ShoeCare, "đôi");

            // 8. Terms of service for customers (first login)
            await SeedTermsPolicyAsync();
        }

        private async Task SeedTermsPolicyAsync()
        {
            if (await _termsRepo.GetCountAsync() > 0)
                return;
            await _termsRepo.InsertAsync(new TermsPolicy(TermsPolicy.DefaultId)
            {
                Version = TermsPolicy.DefaultVersion,
                Title = "Điều khoản sử dụng dịch vụ WashGo",
                Content = DefaultTermsContent,
                EffectiveFrom = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow
            }, autoSave: true);
        }

        private const string DefaultTermsContent =
            "1. Đặt đơn và gửi đồ: sau khi tạo đơn, ô tủ được giữ trong thời hạn gửi đồ (xem mục quy định bên dưới). " +
            "Quá hạn, bạn không thể gửi đồ cho đơn đó và nên hủy đơn để giải phóng ô.\n" +
            "2. Giá và thanh toán: giá tạm tính theo bảng dịch vụ; Merchant cân thực tế và chốt giá cuối cùng. " +
            "Bạn thanh toán trực tuyến (VNPay/MoMo) sau khi giặt xong; quá 7 ngày chưa thanh toán, đồ được chuyển sang lưu kho và tính phí lưu kho.\n" +
            "3. Hủy đơn: miễn phí trong thời hạn gửi đồ khi chưa gửi; sau đó tính phí hủy theo tỷ lệ đang áp dụng.\n" +
            "4. Nhận đồ: đồ được giữ miễn phí tại locker trong thời gian quy định; quá hạn tính phí theo ngày và có thể chuyển về kho.\n" +
            "5. Sự cố và bồi thường: báo cáo sự cố kèm hình ảnh/video trong thời hạn quy định; mức bồi thường theo tỷ lệ giá trị món đồ.\n" +
            "6. Dữ liệu cá nhân: WashGo chỉ dùng thông tin của bạn để xử lý đơn hàng và thông báo liên quan.";

        private async Task<Role> SeedRoleAsync(Guid id, string name, string description)
        {
            var role = await _roleRepo.FindAsync(id);
            if (role == null)
            {
                role = new Role(id)
                {
                    Name = name,
                    Description = description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _roleRepo.InsertAsync(role, autoSave: true);
            }
            return role;
        }

        private async Task SeedUserAsync(Guid id, Guid roleId, string email, string passwordHash, string fullName, string phone)
        {
            var existing = await _userRepo.FindAsync(id);
            if (existing == null)
            {
                var user = new User(id)
                {
                    RoleId = roleId,
                    Email = email,
                    PasswordHash = passwordHash,
                    FullName = fullName,
                    Phone = phone,
                    AuthProvider = AuthProvider.Local,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userRepo.InsertAsync(user, autoSave: true);
            }
            else if (string.IsNullOrEmpty(existing.PasswordHash) || !existing.PasswordHash.StartsWith("$2"))
            {
                existing.PasswordHash = passwordHash;
                existing.UpdatedAt = DateTime.UtcNow;
                await _userRepo.UpdateAsync(existing, autoSave: true);
            }
        }

        private async Task SeedCustomerProfileAsync(Guid userId, string address, string city)
        {
            var existing = await _customerRepo.FindAsync(userId);
            if (existing == null)
            {
                var customer = new Customer(userId)
                {
                    Address = address,
                    City = city,
                    LoyaltyPoints = 0,
                    TotalOrders = 0,
                    TotalSpent = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _customerRepo.InsertAsync(customer, autoSave: true);
            }
        }

        private async Task SeedMerchantProfileAsync(Guid userId, string businessName, string address, string district, string city, string phone)
        {
            var existing = await _merchantRepo.FindAsync(userId);
            if (existing == null)
            {
                var merchant = new Merchant(userId)
                {
                    BusinessName = businessName,
                    Address = address,
                    District = district,
                    City = city,
                    Latitude = 10.7769m,
                    Longitude = 106.7009m,
                    Phone = phone,
                    Status = MerchantStatus.Active,
                    Rating = 5.0m,
                    TotalOrders = 0,
                    TotalRevenue = 0,
                    DepositAmount = 1000000,
                    CommissionRate = 20,
                    ServiceRadiusKm = 5,
                    MaxCapacity = 50,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _merchantRepo.InsertAsync(merchant, autoSave: true);
            }
        }

        private async Task SeedShipperProfileAsync(Guid userId, Guid merchantId, string vehiclePlate)
        {
            var existing = await _shipperRepo.FirstOrDefaultAsync(s => s.UserId == userId);
            if (existing == null)
            {
                var shipper = new Shipper
                {
                    UserId = userId,
                    MerchantId = merchantId,
                    VehicleType = VehicleType.Motorbike,
                    VehiclePlate = vehiclePlate,
                    Status = ShipperStatus.Available,
                    IsActive = true,
                    Rating = 0m, // Shipper star ratings were dropped; 0 = none.
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _shipperRepo.InsertAsync(shipper, autoSave: true);
            }
        }

        private async Task SeedServiceAreaAsync(Guid id, string code, string name, string district, string city, decimal lat, decimal lng, decimal radiusKm)
        {
            var existing = await _serviceAreaRepo.FindAsync(id);
            if (existing == null)
            {
                var area = new ServiceArea(id)
                {
                    Code = code,
                    Name = name,
                    District = district,
                    City = city,
                    Latitude = lat,
                    Longitude = lng,
                    RadiusKm = radiusKm,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _serviceAreaRepo.InsertAsync(area, autoSave: true);
            }
        }

        private async Task SeedMerchantServiceAreaAsync(Guid id, Guid merchantId, Guid serviceAreaId)
        {
            var existing = await _merchantServiceAreaRepo.FindAsync(id);
            if (existing == null)
            {
                var msa = new MerchantServiceArea(id)
                {
                    MerchantId = merchantId,
                    ServiceAreaId = serviceAreaId,
                    Priority = 1,
                    IsActive = true,
                    AssignedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _merchantServiceAreaRepo.InsertAsync(msa, autoSave: true);
            }
        }

        private async Task SeedLockerAsync(Guid lockerId, Guid merchantId, Guid serviceAreaId, string code, string name, string address, string district, string city)
        {
            var existing = await _lockerRepo.FindAsync(lockerId);
            if (existing == null)
            {
                var locker = new Locker(lockerId)
                {
                    ServiceAreaId = serviceAreaId,
                    Code = code,
                    Name = name,
                    Address = address,
                    District = district,
                    City = city,
                    Latitude = 10.7769,
                    Longitude = 106.7009,
                    TotalBoxes = 3,
                    AvailableBoxes = 3,
                    Status = LockerStatus.Active,
                    LockerType = LockerType.Normal,
                    Description = "Trạm tủ đồ thông minh 24/7 Bến Thành"
                };
                locker.Boxes.Add(new LockerBox(Guid.Parse("b1111111-1111-1111-1111-111111111111"))
                {
                    LockerId = lockerId,
                    SlotNumber = 1,
                    Size = LockerBoxSize.Standard,
                    Status = LockerBoxStatus.Available
                });
                locker.Boxes.Add(new LockerBox(Guid.Parse("b2222222-2222-2222-2222-222222222222"))
                {
                    LockerId = lockerId,
                    SlotNumber = 2,
                    Size = LockerBoxSize.Small,
                    Status = LockerBoxStatus.Available
                });
                locker.Boxes.Add(new LockerBox(Guid.Parse("b3333333-3333-3333-3333-333333333333"))
                {
                    LockerId = lockerId,
                    SlotNumber = 3,
                    Size = LockerBoxSize.Large,
                    Status = LockerBoxStatus.Available
                });

                await _lockerRepo.InsertAsync(locker, autoSave: true);
            }
            await SeedLockerMerchantAsync(lockerId, merchantId);
        }

        // Lockers may have several Merchants; the seed locker starts with the seed Merchant (priority 1).
        private async Task SeedLockerMerchantAsync(Guid lockerId, Guid merchantId)
        {
            if (!await _lockerMerchantRepo.AnyAsync(x => x.LockerId == lockerId && x.MerchantId == merchantId))
                await _lockerMerchantRepo.InsertAsync(new LockerMerchant(Guid.NewGuid())
                {
                    LockerId = lockerId,
                    MerchantId = merchantId,
                    Priority = 1
                }, autoSave: true);
        }

        private async Task SeedReturnLockerAsync(Guid merchantId, Guid serviceAreaId)
        {
            var lockerId = Guid.Parse("a2222222-aaaa-2222-aaaa-222222222222");
            if (await _lockerRepo.FindAsync(lockerId) != null)
                return;

            var locker = new Locker(lockerId)
            {
                ServiceAreaId = serviceAreaId,
                Code = "LK_Q1_002",
                Name = "Trạm Locker Nhà thờ Đức Bà",
                Address = "01 Công xã Paris",
                District = "Quận 1",
                City = "TP. Hồ Chí Minh",
                Latitude = 10.7798,
                Longitude = 106.6990,
                TotalBoxes = 2,
                AvailableBoxes = 2,
                Status = LockerStatus.Active,
                LockerType = LockerType.Normal,
                Description = "Trạm tủ đồ thông minh 24/7 Nhà thờ Đức Bà"
            };
            locker.Boxes.Add(new LockerBox(Guid.Parse("c1111111-aaaa-1111-aaaa-111111111111"))
            {
                LockerId = lockerId,
                SlotNumber = 1,
                Size = LockerBoxSize.Standard,
                Status = LockerBoxStatus.Available
            });
            locker.Boxes.Add(new LockerBox(Guid.Parse("c2222222-aaaa-2222-aaaa-222222222222"))
            {
                LockerId = lockerId,
                SlotNumber = 2,
                Size = LockerBoxSize.Large,
                Status = LockerBoxStatus.Available
            });
            await _lockerRepo.InsertAsync(locker, autoSave: true);
            await SeedLockerMerchantAsync(lockerId, merchantId);
        }

        private async Task SeedWashServiceAsync(Guid id, Guid merchantId, string code, string name, decimal unitPrice, decimal? pricePerKg, decimal? pricePerItem, WashServiceType type, string unit)
        {
            var existing = await _washServiceRepo.FindAsync(id);
            if (existing == null)
            {
                var svc = new WashService(id)
                {
                    MerchantId = merchantId,
                    Code = code,
                    Name = name,
                    UnitPrice = unitPrice,
                    PricePerKg = pricePerKg,
                    PricePerItem = pricePerItem,
                    ServiceType = type,
                    Unit = unit,
                    EstimatedDurationHours = 24,
                    ApprovalStatus = ServiceApprovalStatus.Approved,
                    IsActive = true
                };
                await _washServiceRepo.InsertAsync(svc, autoSave: true);
            }
        }
    }
}
