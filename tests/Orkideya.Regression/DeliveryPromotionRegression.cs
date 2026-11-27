using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Orders;
using Orkideya.Application.Shipping;
using Orkideya.Controllers;
using Orkideya.Data;
using Orkideya.Models;

/// <summary>Free-delivery promotion: the single server-side fee rule and its guards.</summary>
internal static class DeliveryPromotionRegression
{
    public static async Task Run(Action<bool, string> check)
    {
        var now = new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);
        DeliveryPromotion Promo(string scope = DeliveryPromotion.ScopeTripoli, decimal? min = null, bool enabled = true) => new()
        {
            Title = "عرض الجمعة البيضاء", IsEnabled = enabled, Scope = scope, MinSubtotal = min,
            StartsAtUtc = now.AddHours(-2), EndsAtUtc = now.AddHours(10)
        };
        var p = Promo();

        check(DeliveryPromotionRule.Apply(p, now, 15m, "طرابلس", null, 50m) == new PromotedFee(0m, 15m), "Tripoli city is free inside the window");
        check(DeliveryPromotionRule.Apply(p, now, 15m, "طرابلس — جنزور", "طرابلس", 50m).Fee == 0m, "Imported Tripoli area is free");
        check(DeliveryPromotionRule.Apply(p, now, 15m, "جنزور", "طرابلس", 50m).Fee == 0m, "Area whose region is Tripoli is free");
        check(DeliveryPromotionRule.Apply(p, now, 20m, "مصراتة — الزروق", "مصراتة", 50m) == new PromotedFee(20m, null), "Other city pays the normal fee");
        check(DeliveryPromotionRule.Apply(p, now, 15m, "ضواحي طرابلس", "المنطقة الغربية", 50m).Fee == 15m, "Tripoli suburbs rate is not Tripoli");
        check(DeliveryPromotionRule.Apply(Promo(DeliveryPromotion.ScopeAllLibya), now, 20m, "مصراتة", "مصراتة", 50m).Fee == 0m, "All-Libya scope frees every city");
        check(DeliveryPromotionRule.Apply(p, p.StartsAtUtc, 15m, "طرابلس", null, 50m).Fee == 0m, "Window start is inclusive");
        check(DeliveryPromotionRule.Apply(p, p.EndsAtUtc, 15m, "طرابلس", null, 50m).Fee == 15m, "Window end is exclusive");
        check(DeliveryPromotionRule.Apply(p, p.StartsAtUtc.AddTicks(-1), 15m, "طرابلس", null, 50m).Fee == 15m, "Before the window pays the normal fee");
        check(DeliveryPromotionRule.Apply(Promo(enabled: false), now, 15m, "طرابلس", null, 50m).Fee == 15m, "Disabled promotion never applies");
        check(DeliveryPromotionRule.Apply(null, now, 15m, "طرابلس", null, 50m).Fee == 15m, "No promotion row means the normal fee");
        check(DeliveryPromotionRule.Apply(Promo(min: 100m), now, 15m, "طرابلس", null, 99.99m).Fee == 15m, "Below the minimum subtotal pays the normal fee");
        check(DeliveryPromotionRule.Apply(Promo(min: 100m), now, 15m, "طرابلس", null, 100m).Fee == 0m, "Reaching the minimum subtotal is free");
        check(DeliveryPromotionRule.Apply(p, now, 0m, "طرابلس", null, 50m) == new PromotedFee(0m, null), "Office pickup already 0 stays 0 with no discount recorded");
        check(DeliveryPromotionRule.Apply(new DeliveryPromotion { Title = "x", IsEnabled = true, Scope = "Mars",
            StartsAtUtc = p.StartsAtUtc, EndsAtUtc = p.EndsAtUtc }, now, 15m, "طرابلس", null, 50m).Fee == 15m, "Unknown scope never applies");

        // Admin input: Tripoli time (UTC+2) in, UTC stored.
        var error = AdminPromotionsController.Validate(new(true, " عرض الجمعة البيضاء ", "Tripoli", "2026-11-27T00:00", "2026-11-30T23:59", ""), out var parsed);
        check(error is null && parsed.Title == "عرض الجمعة البيضاء" && parsed.StartsAtUtc == new DateTime(2026, 11, 26, 22, 0, 0) &&
            parsed.EndsAtUtc == new DateTime(2026, 11, 30, 21, 59, 0) && parsed.MinSubtotal is null, "Admin times are read as Tripoli time and stored in UTC");
        check(AdminPromotionsController.Local(parsed.StartsAtUtc) == "2026-11-27T00:00", "Admin form shows stored UTC back in Tripoli time");
        check(AdminPromotionsController.Validate(new(true, "x", "Tripoli", "2026-11-30T00:00", "2026-11-29T00:00", null), out _) is not null, "End before start is rejected");
        check(AdminPromotionsController.Validate(new(true, new string('a', 81), "Tripoli", "2026-11-27T00:00", "2026-11-28T00:00", null), out _) is not null, "Title over 80 characters is rejected");
        check(AdminPromotionsController.Validate(new(true, "x", "Benghazi", "2026-11-27T00:00", "2026-11-28T00:00", null), out _) is not null, "Unknown scope is rejected");
        check(AdminPromotionsController.Validate(new(true, "x", "AllLibya", "2026-11-27T00:00", "2026-11-28T00:00", "-1"), out _) is not null, "Negative minimum is rejected");
        check(AdminPromotionsController.Validate(new(true, "x", "AllLibya", "2026-11-27T00:00", "2026-11-28T00:00", "150"), out var withMin) is null && withMin.MinSubtotal == 150m, "Minimum subtotal is accepted");
        check(AdminPromotionsController.Status(p, now) == "active" && AdminPromotionsController.Status(p, p.EndsAtUtc) == "ended" &&
            AdminPromotionsController.Status(p, p.StartsAtUtc.AddMinutes(-1)) == "scheduled" && AdminPromotionsController.Status(Promo(enabled: false), now) == "stopped",
            "Admin status reads active, scheduled, ended and stopped");
        check(typeof(AdminPromotionsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().Any(a => a.Roles == "Admin") &&
            typeof(AdminPromotionsController).GetMethod(nameof(AdminPromotionsController.Save))!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).Length == 1,
            "Promotion admin API is admin-only and saving requires antiforgery");

        // Order aggregate guard: the promotion record is only valid on a website order charged 0.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=OrkideaNoDb;Integrated Security=true;Connect Timeout=1").Options;
        await using var db = new ApplicationDbContext(options);
        Order Draft(decimal cost, decimal? original, string source) => new()
        {
            CustomerName = "Synthetic", WhatsAppNumber = "+218000000000", ShippingAddress = "Synthetic", City = "طرابلس",
            ShippingCost = cost, OriginalShippingCost = original, TotalAmount = 30m + cost, CheckoutToken = Guid.NewGuid(),
            CheckoutRequestHash = new string('a', 64),
            Creation = new OrderCreation { Source = source, ActorUserId = source == OrderSources.Facebook ? "admin" : null },
            Items = [new OrderItem { ProductId = 1, Quantity = 1, UnitPrice = 30m }]
        };
        async Task<bool> Rejected(Order order)
        {
            try { await new OrderService(db).PlaceAsync(order); return false; }
            catch (ArgumentException exception) { return exception.Message.StartsWith("Invalid promotion", StringComparison.Ordinal); }
            catch (Exception) { return false; }
        }
        check(await Rejected(Draft(5m, 15m, OrderSources.Website)), "Promotion record with a charged fee is rejected");
        check(await Rejected(Draft(0m, 15m, OrderSources.Facebook)), "Manual Facebook orders never carry a promotion record");
        check(await Rejected(Draft(0m, -1m, OrderSources.Website)), "Negative normal fee is rejected");
    }
}
