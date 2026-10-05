using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;
using WashGo.EntityFrameworkCore.BuilderConfiguration;

namespace WashGo.EntityFrameworkCore.EntityFrameworkCore
{
    [ConnectionStringName("Default")]
    public class WashGoDbContext : AbpDbContext<WashGoDbContext>
    {
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<OtpCode> OtpCodes { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Merchant> Merchants { get; set; } = null!;
        public DbSet<Shipper> Shippers { get; set; } = null!;
        public DbSet<ServiceArea> ServiceAreas { get; set; } = null!;
        public DbSet<MerchantServiceArea> MerchantServiceAreas { get; set; } = null!;
        public DbSet<MerchantImage> MerchantImages { get; set; } = null!;
        public DbSet<ServiceChangeRequest> ServiceChangeRequests { get; set; } = null!;
        public DbSet<WashOrder> WashOrders { get; set; } = null!;
        public DbSet<WashOrderItem> WashOrderItems { get; set; } = null!;
        public DbSet<WashOrderTimeline> WashOrderTimelines { get; set; } = null!;
        public DbSet<Locker> Lockers { get; set; } = null!;
        public DbSet<LockerBox> LockerBoxes { get; set; } = null!;
        public DbSet<WashService> WashServices { get; set; } = null!;

        public WashGoDbContext(DbContextOptions<WashGoDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.HasDefaultSchema(WashGoDbProperties.Schema);

            builder.ApplyConfiguration(new RoleEntityTypeConfiguration());
            builder.ApplyConfiguration(new UserEntityTypeConfiguration());
            builder.ApplyConfiguration(new RefreshTokenEntityTypeConfiguration());
            builder.ApplyConfiguration(new OtpCodeEntityTypeConfiguration());
            builder.ApplyConfiguration(new CustomerEntityTypeConfiguration());
            builder.ApplyConfiguration(new MerchantEntityTypeConfiguration());
            builder.ApplyConfiguration(new ShipperEntityTypeConfiguration());
            builder.ApplyConfiguration(new ServiceAreaEntityTypeConfiguration());
            builder.ApplyConfiguration(new MerchantServiceAreaEntityTypeConfiguration());
            builder.ApplyConfiguration(new MerchantImageEntityTypeConfiguration());
            builder.ApplyConfiguration(new ServiceChangeRequestEntityTypeConfiguration());
            builder.ApplyConfiguration(new WashOrderEntityTypeConfiguration());
            builder.ApplyConfiguration(new WashOrderItemEntityTypeConfiguration());
            builder.ApplyConfiguration(new WashOrderTimelineEntityTypeConfiguration());
            builder.ApplyConfiguration(new LockerEntityTypeConfiguration());
            builder.ApplyConfiguration(new LockerBoxEntityTypeConfiguration());
            builder.ApplyConfiguration(new WashServiceEntityTypeConfiguration());

            builder.UseWashGoSnakeCaseColumns();
        }
    }
}
