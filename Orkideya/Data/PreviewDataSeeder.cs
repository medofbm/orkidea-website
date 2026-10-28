using Microsoft.EntityFrameworkCore;
using Orkideya.Models;

namespace Orkideya.Data;

public static class PreviewDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        if (!environment.IsStaging() || context.Database.GetDbConnection().Database != "OrkideaStaging")
            throw new InvalidOperationException("Preview data requires the dedicated OrkideaStaging database and Staging environment.");
        if (await context.Products.AnyAsync()) return;
        if (await context.Orders.AnyAsync()) throw new InvalidOperationException("Refusing preview data in a database containing orders.");
        foreach (var name in new[] { "زيت الحبة السوداء", "زيت السمسم", "زيت جوز الهند", "صابون الحبة السوداء" })
        {
            var product = new Product { Name = name, ImageUrl = "/images/logo.jpg",
                Description = "منتج للمعاينة فقط. السعر والحجم الظاهران بيانات اختبار غير معتمدة للبيع." };
            product.ProductVariants.Add(new ProductVariant { Product = product, Size = "حجم تجريبي", Price = 1m });
            context.Products.Add(product);
        }
        if (!await context.ShippingRates.AnyAsync())
            context.ShippingRates.Add(new ShippingRate { CityName = "طرابلس — تجربة", Price = 1m,
                Region = "اختبار", DeliveryDuration = "لا يوجد توصيل فعلي" });
        await context.SaveChangesAsync();
    }
}
