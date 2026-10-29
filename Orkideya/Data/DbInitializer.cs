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

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(email))
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException("Bootstrap password is required for a new administrator.");
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                EnsureSucceeded(await userManager.CreateAsync(user, password));
            }
            if (!await roleManager.RoleExistsAsync("Admin"))
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole("Admin")));
            if (!await userManager.IsInRoleAsync(user, "Admin"))
                EnsureSucceeded(await userManager.AddToRoleAsync(user, "Admin"));
        }
        else if (!string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Bootstrap email is required.");
        }
        if (configuration.GetValue<bool>("Initialization:SeedPreviewData"))
            await PreviewDataSeeder.SeedAsync(context, serviceProvider.GetRequiredService<IWebHostEnvironment>());
        else await SeedShippingRatesAsync(context);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Administrator bootstrap failed: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
    }

    public static async Task SeedShippingRatesAsync(ApplicationDbContext context)
    {
        var defaultCities = new List<ShippingRate>
        {
            // المنطقة الغربية
            new ShippingRate { CityName = "طرابلس", Price = 10.00m, Region = "المنطقة الغربية", DeliveryDuration = "خلال 24 ساعة" },
            new ShippingRate { CityName = "ضواحي طرابلس", Price = 15.00m, Region = "المنطقة الغربية", DeliveryDuration = "خلال 24 ساعة" },
            new ShippingRate { CityName = "مصراتة", Price = 20.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "زليتن", Price = 20.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "الخمس", Price = 20.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "الزاوية", Price = 20.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "ورشفانة", Price = 25.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "صرمان", Price = 25.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "صبراتة", Price = 25.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "العجيلات", Price = 30.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "زوارة", Price = 30.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "زلطان", Price = 35.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "رقدالين", Price = 35.00m, Region = "المنطقة الغربية", DeliveryDuration = "ساعة 48-24" },

            // الجبل الغربي
            new ShippingRate { CityName = "غريان", Price = 25.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "ككلة", Price = 30.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "الرجبان", Price = 35.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "الزنتان", Price = 35.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "نالوت", Price = 40.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "تيجي", Price = 40.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "الحرابة", Price = 35.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "الرياينة", Price = 35.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "يفرن", Price = 35.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "جادو", Price = 35.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "قلعة", Price = 35.00m, Region = "الجبل الغربي", DeliveryDuration = "ساعة 48-24" },

            // المنطقة الوسطى
            new ShippingRate { CityName = "ترهونة", Price = 30.00m, Region = "المنطقة الوسطى", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "بني وليد", Price = 30.00m, Region = "المنطقة الوسطى", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "مسلاتة", Price = 30.00m, Region = "المنطقة الوسطى", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "سرت", Price = 30.00m, Region = "المنطقة الوسطى", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "تاورغاء", Price = 30.00m, Region = "المنطقة الوسطى", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "البريقة", Price = 30.00m, Region = "المنطقة الوسطى", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "بن جواد", Price = 30.00m, Region = "المنطقة الوسطى", DeliveryDuration = "ساعة 48-24" },

            // المنطقة الجنوبية
            new ShippingRate { CityName = "الجفرة", Price = 40.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "هون", Price = 30.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "سوكنا", Price = 30.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "زلة", Price = 40.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "براك الشاطئ", Price = 35.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "سبها", Price = 30.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "أوباري", Price = 45.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "الكفرة", Price = 45.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "2-4 أيام" },
            new ShippingRate { CityName = "أم الأرانب", Price = 45.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "2-4 أيام" },
            new ShippingRate { CityName = "تراغن", Price = 45.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "2-4 أيام" },
            new ShippingRate { CityName = "مرزق", Price = 45.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "2-4 أيام" },
            new ShippingRate { CityName = "غات", Price = 50.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "2-4 أيام" },
            new ShippingRate { CityName = "ودان", Price = 30.00m, Region = "المنطقة الجنوبية", DeliveryDuration = "ساعة 72-48" },

            // المنطقة الشرقية
            new ShippingRate { CityName = "بنغازي", Price = 25.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "ضواحي بنغازي", Price = 25.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 48-24" },
            new ShippingRate { CityName = "اجدابيا", Price = 30.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "المرج", Price = 30.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "البيضاء", Price = 35.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "قمينس", Price = 30.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "توكرة", Price = 30.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "شحات", Price = 30.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "سوسة", Price = 30.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "درنة", Price = 40.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" },
            new ShippingRate { CityName = "طبرق", Price = 35.00m, Region = "المنطقة الشرقية", DeliveryDuration = "ساعة 72-48" }
        };

        var existingNames = (await context.ShippingRates.Select(rate => rate.CityName).ToListAsync())
            .Select(name => name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await context.ShippingRates.AddRangeAsync(defaultCities.Where(rate => existingNames.Add(rate.CityName.Trim())));
        await context.SaveChangesAsync();
    }
}
