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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
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
