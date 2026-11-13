using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Policy;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Finance;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.Promotion;
using WashGo.Domain.Entities.SystemConfig;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;
using WashGo.EntityFrameworkCore.BuilderConfiguration;
using WashGo.Domain.Entities.Audit;
using WashGo.Domain.Entities.Chatbot;
using System.Text.RegularExpressions;
using WashGo.Domain.Shared.Validation;

namespace WashGo.EntityFrameworkCore.EntityFrameworkCore
{
    [ConnectionStringName("Default")]
    public class WashGoDbContext : AbpDbContext<WashGoDbContext>
    {
        public DbSet<OrderStorageCycle> StorageCycles { get; set; } = null!;
        public DbSet<StorageReturnRequest> StorageReturnRequests { get; set; } = null!;
        public DbSet<ChatConversation> ChatConversations { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<OtpCode> OtpCodes { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Merchant> Merchants { get; set; } = null!;
        public DbSet<PrivateDocument> PrivateDocuments { get; set; } = null!;
        public DbSet<UploadedAsset> UploadedAssets { get; set; } = null!;
        public DbSet<AdministrativeOperation> AdministrativeOperations { get; set; } = null!;
        public DbSet<MerchantWriteOperation> MerchantWriteOperations { get; set; } = null!;
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
        public DbSet<LockerMerchant> LockerMerchants { get; set; } = null!;
        public DbSet<WashService> WashServices { get; set; } = null!;
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
        public DbSet<RefundRequest> RefundRequests { get; set; } = null!;
        public DbSet<Promotion> Promotions { get; set; } = null!;
        public DbSet<PromotionUsage> PromotionUsages { get; set; } = null!;
        public DbSet<OrderRating> OrderRatings { get; set; } = null!;
        public DbSet<Incident> Incidents { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public DbSet<ReminderLog> ReminderLogs { get; set; } = null!;
        public DbSet<MerchantSettlement> MerchantSettlements { get; set; } = null!;
        public DbSet<MerchantProfileRequest> MerchantProfileRequests { get; set; } = null!;
        public DbSet<TermsPolicy> TermsPolicies { get; set; } = null!;
        public DbSet<TermsAcceptance> TermsAcceptances { get; set; } = null!;
        public DbSet<IncidentMessage> IncidentMessages { get; set; } = null!;
        public DbSet<OrderOperation> OrderOperations { get; set; } = null!;
        public DbSet<ShipperAudit> ShipperAudits { get; set; } = null!;
        public DbSet<DeliveryTask> DeliveryTasks { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        public WashGoDbContext(DbContextOptions<WashGoDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.HasDefaultSchema(WashGoDbProperties.Schema);
            builder.ApplyConfiguration(new ChatConversationEntityTypeConfiguration());
            builder.ApplyConfiguration(new ChatMessageEntityTypeConfiguration());

            builder.ApplyConfiguration(new RoleEntityTypeConfiguration());
            builder.ApplyConfiguration(new UserEntityTypeConfiguration());
            builder.ApplyConfiguration(new RefreshTokenEntityTypeConfiguration());
            builder.ApplyConfiguration(new OtpCodeEntityTypeConfiguration());
            builder.ApplyConfiguration(new CustomerEntityTypeConfiguration());
            builder.ApplyConfiguration(new MerchantEntityTypeConfiguration());
            builder.ApplyConfiguration(new PrivateDocumentEntityTypeConfiguration());
            builder.ApplyConfiguration(new UploadedAssetEntityTypeConfiguration());
            builder.ApplyConfiguration(new AdministrativeOperationEntityTypeConfiguration());
            builder.ApplyConfiguration(new MerchantWriteOperationEntityTypeConfiguration());
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
            builder.ApplyConfiguration(new PaymentTransactionEntityTypeConfiguration());
            builder.ApplyConfiguration(new RefundRequestEntityTypeConfiguration());
            builder.ApplyConfiguration(new PromotionEntityTypeConfiguration());
            builder.ApplyConfiguration(new PromotionUsageEntityTypeConfiguration());
            builder.ApplyConfiguration(new OrderRatingEntityTypeConfiguration());
            builder.ApplyConfiguration(new IncidentEntityTypeConfiguration());
            builder.ApplyConfiguration(new NotificationEntityTypeConfiguration());
            builder.ApplyConfiguration(new SystemSettingEntityTypeConfiguration());
            builder.ApplyConfiguration(new ReminderLogEntityTypeConfiguration());
            builder.ApplyConfiguration(new TermsPolicyEntityTypeConfiguration());
            builder.ApplyConfiguration(new TermsAcceptanceEntityTypeConfiguration());
            builder.ApplyConfiguration(new OrderOperationEntityTypeConfiguration());
            builder.ApplyConfiguration(new ShipperAuditEntityTypeConfiguration());
            builder.ApplyConfiguration(new DeliveryTaskEntityTypeConfiguration());
            builder.ApplyConfiguration(new OrderStorageCycleEntityTypeConfiguration());
            builder.ApplyConfiguration(new StorageReturnRequestEntityTypeConfiguration());
            builder.ApplyConfiguration(new AuditLogEntityTypeConfiguration());
            builder.ApplyConfiguration(new IncidentMessageEntityTypeConfiguration());
            builder.ApplyConfiguration(new MerchantSettlementEntityTypeConfiguration());
            builder.ApplyConfiguration(new MerchantProfileRequestEntityTypeConfiguration());
            builder.ApplyConfiguration(new LockerMerchantEntityTypeConfiguration());

            builder.UseWashGoSnakeCaseColumns();
        }

        private static readonly Regex AssetUrl = new(@"/uploads/[0-9a-f]{32}/[0-9a-f]{32}\.(jpg|png|webp|mp4|webm|mov)", RegexOptions.Compiled);
        private static readonly HashSet<string> AssetOwners = ["User", "Merchant", "MerchantImage", "Locker", "WashService", "ServiceChangeRequest", "MerchantProfileRequest", "OrderRating", "Incident", "IncidentMessage", "Shipper"];
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            // Reference retention is part of the SAME DB transaction as the attaching record. Cleanup uses the same lock.
            var urls = ChangeTracker.Entries().Where(x => (x.State is EntityState.Added or EntityState.Modified) && AssetOwners.Contains(x.Metadata.ClrType.Name))
                .SelectMany(x => x.Properties.SelectMany(p => p.CurrentValue switch
                {
                    string value => AssetUrl.Matches(value).Select(m => m.Value),
                    IEnumerable<string> values => values.SelectMany(v => AssetUrl.Matches(v ?? string.Empty).Select(m => m.Value)),
                    _ => Enumerable.Empty<string>()
                })).Distinct().ToArray();
            if (urls.Length > 0)
            {
                var ids = await UploadedAssets.AsNoTracking().Where(x => urls.Contains(x.Url)).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);
                foreach (var id in ids)
                {
                    var key = $"upload-asset:{id}";
                    await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
                    var now = DateTime.UtcNow;
                    var changed = await UploadedAssets.Where(x => x.Id == id && !x.IsDeleted && (x.ReferencedAt != null || x.ExpiresAt > now))
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReferencedAt, x => x.ReferencedAt ?? now), cancellationToken);
                    if (changed != 1) throw new UploadAttachmentExpiredException();
                }
            }
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
