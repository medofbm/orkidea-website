using Microsoft.AspNetCore.Identity;
using Orkideya.Data;

public static class DbInitializer
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

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
    }
}