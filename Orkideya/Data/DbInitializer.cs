using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

public static class DbInitializer
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // 0. Apply any pending DB migrations automatically (crucial for Azure)
        if (context.Database.IsSqlServer())
        {
            await context.Database.MigrateAsync();
        }

        // --- معلومات حساب المدير ---
        string adminEmail = "medobaghny@gmail.com";
        string adminPassword = "Themen2004fb@";
        // -------------------------

        // 1. إنشاء صلاحية "Admin" إذا لم تكن موجودة
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // 2. البحث عن حساب المدير
        IdentityUser adminUser = await userManager.FindByEmailAsync(adminEmail);

        // 3. إذا لم يكن موجوداً، قم بإنشائه
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, adminPassword);
        }

        // 4. التأكد من أن المستخدم لديه صلاحية "Admin"
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        // 5. Seed Shipping Rates if empty
        if (!await context.ShippingRates.AnyAsync())
        {
            var defaultCities = new List<ShippingRate>
            {
                new ShippingRate { CityName = "طرابلس", Price = 15.00m, Region = "المنطقة الغربية", DeliveryDuration = "24-48 ساعة" },
                new ShippingRate { CityName = "بنغازي", Price = 25.00m, Region = "المنطقة الشرقية", DeliveryDuration = "3-5 أيام" },
                new ShippingRate { CityName = "مصراتة", Price = 15.00m, Region = "المنطقة الغربية", DeliveryDuration = "24-48 ساعة" },
                new ShippingRate { CityName = "الزاوية", Price = 15.00m, Region = "المنطقة الغربية", DeliveryDuration = "24-48 ساعة" },
                new ShippingRate { CityName = "زليتن", Price = 15.00m, Region = "المنطقة الغربية", DeliveryDuration = "24-48 ساعة" },
                new ShippingRate { CityName = "الخمس", Price = 15.00m, Region = "المنطقة الغربية", DeliveryDuration = "24-48 ساعة" },
                new ShippingRate { CityName = "صبراتة", Price = 20.00m, Region = "المنطقة الغربية", DeliveryDuration = "24-48 ساعة" },
                new ShippingRate { CityName = "سبها", Price = 30.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "4-6 أيام" }
            };

            await context.ShippingRates.AddRangeAsync(defaultCities);
            await context.SaveChangesAsync();
        }
    }
}