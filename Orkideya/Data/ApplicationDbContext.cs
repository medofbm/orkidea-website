using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // <<<--- أضف هذا السطر
using Microsoft.EntityFrameworkCore;
using Orkideya.Models;

namespace Orkideya.Data
{
    // غيرنا هنا من DbContext إلى IdentityDbContext
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ShippingRate> ShippingRates { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<ProductVariant> ProductVariants { get; set; }

        public DbSet<Category> Categories { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<OrderNotification> OrderNotifications { get; set; }

        public DbSet<StockAccount> StockAccounts { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }

        public DbSet<OrderStatusChange> OrderStatusChanges { get; set; }
        public DbSet<OrderDelivery> OrderDeliveries { get; set; }
        public DbSet<OrderDeliveryEvent> OrderDeliveryEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            const string deliveryMetadata = "[Mode] IN ('Company','Self') AND (([Mode]='Company' AND [CarrierName] IS NOT NULL AND LEN(LTRIM(RTRIM([CarrierName])))>0) OR ([Mode]='Self' AND [CarrierName] IS NULL)) AND ([ShipmentReference] IS NULL OR LEN(LTRIM(RTRIM([ShipmentReference])))>0)";
            builder.Entity<OrderDelivery>().Property(d => d.OrderId).ValueGeneratedNever();
            builder.Entity<OrderDeliveryEvent>().Property(e => e.RequestId).ValueGeneratedNever();
            builder.Entity<OrderDelivery>().HasOne<Order>().WithOne().HasForeignKey<OrderDelivery>(d => d.OrderId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<OrderDelivery>().ToTable(t => t.HasCheckConstraint("CK_OrderDelivery_State",
                deliveryMetadata + " AND [State] IN ('HandedOver','Delivered','Returning','Returned') AND [Revision]>0 AND [UpdatedAtUtc]>=[CreatedAtUtc]"));
            builder.Entity<OrderDelivery>().HasIndex(d => new { d.State, d.UpdatedAtUtc });
            builder.Entity<OrderDeliveryEvent>().HasOne<OrderDelivery>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<OrderDeliveryEvent>().HasIndex(e => new { e.OrderId, e.Revision }).IsUnique();
            builder.Entity<OrderDeliveryEvent>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_OrderDeliveryEvent_Data", deliveryMetadata + " AND [Revision]>0 AND LEN(LTRIM(RTRIM([ActorUserId])))>0 AND LEN([RequestHash])=64 AND [RequestId]<>'00000000-0000-0000-0000-000000000000' AND ([LegacyPreparationConfirmed]=0 OR [Action]='HandOver') AND [ReturnReceiptConfirmed]=CASE WHEN [Action]='ReceiveReturn' THEN 1 ELSE 0 END AND [SamePackageReuseConfirmed]=CASE WHEN [Action]='Redispatch' THEN 1 ELSE 0 END AND ([Action] NOT IN ('MarkReturning','Resume','Redispatch','CorrectDetails','CorrectState') OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason])))>0))");
                t.HasCheckConstraint("CK_OrderDeliveryEvent_Transition",
                    "([Action]='HandOver' AND [PreviousState] IS NULL AND [NewState]='HandedOver') OR ([PreviousState] IS NOT NULL AND (([Action]='Deliver' AND [PreviousState]='HandedOver' AND [NewState]='Delivered') OR ([Action]='MarkReturning' AND [PreviousState]='HandedOver' AND [NewState]='Returning') OR ([Action]='ReceiveReturn' AND [PreviousState]='Returning' AND [NewState]='Returned') OR ([Action]='Resume' AND [PreviousState]='Returning' AND [NewState]='HandedOver') OR ([Action]='Redispatch' AND [PreviousState]='Returned' AND [NewState]='HandedOver') OR ([Action]='CorrectDetails' AND [PreviousState]=[NewState] AND [NewState] IN ('HandedOver','Delivered','Returning','Returned')) OR ([Action]='CorrectState' AND (([PreviousState]='Delivered' AND [NewState]='HandedOver') OR ([PreviousState]='Returned' AND [NewState]='Returning')))))");
            });
            builder.Entity<Order>().Property(o => o.StockState).HasDefaultValue("Untracked");
            builder.Entity<StockAccount>().HasOne<Product>().WithOne().HasForeignKey<StockAccount>(stock => stock.ProductId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<StockAccount>().ToTable(table => table.HasCheckConstraint("CK_StockAccount_Balances",
                "[OnHand] >= 0 AND [Reserved] >= 0 AND [Reserved] <= [OnHand] AND [OnHand] <= 1000000000 AND [Unit] IN ('ml','piece') AND ([Unit] <> 'piece' OR ([OnHand] = FLOOR([OnHand]) AND [Reserved] = FLOOR([Reserved])))"));
            builder.Entity<StockMovement>().HasOne<StockAccount>().WithMany().HasForeignKey(move => move.StockAccountId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<StockMovement>().HasOne<Order>().WithMany().HasForeignKey(move => move.OrderId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<StockMovement>().HasIndex(move => new { move.OrderId, move.StockAccountId, move.Kind }).IsUnique().HasFilter("[OrderId] IS NOT NULL");
            builder.Entity<OrderItem>().HasOne<StockAccount>().WithMany().HasForeignKey(item => item.StockAccountId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<OrderItem>().ToTable(table => table.HasCheckConstraint("CK_OrderItem_StockSnapshot",
                "([StockAccountId] IS NULL AND [StockUnitsPerItem] IS NULL AND [StockUnit] IS NULL) OR ([StockAccountId] IS NOT NULL AND [StockAccountId] = [ProductId] AND [StockUnitsPerItem] IS NOT NULL AND [StockUnitsPerItem] > 0 AND [StockUnitsPerItem] <= 1000000 AND [StockUnit] IS NOT NULL AND [StockUnit] IN ('ml','piece') AND ([StockUnit] <> 'piece' OR [StockUnitsPerItem] = FLOOR([StockUnitsPerItem])))"));
            builder.Entity<OrderStatusChange>().HasOne<Order>().WithMany()
                .HasForeignKey(change => change.OrderId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<OrderStatusChange>().HasIndex(change => new { change.OrderId, change.Revision }).IsUnique();
            builder.Entity<Order>().HasIndex(order => order.CheckoutToken).IsUnique()
                .HasFilter("[CheckoutToken] IS NOT NULL");
            builder.Entity<Order>().HasMany(order => order.Items).WithOne()
                .HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<Order>().HasMany(order => order.Notifications).WithOne(notification => notification.Order)
                .HasForeignKey(notification => notification.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<OrderNotification>().HasIndex(notification => notification.OrderId).IsUnique();
            builder.Entity<OrderNotification>().HasIndex(notification => new { notification.State, notification.NextAttemptAtUtc });
        }
    }
}
