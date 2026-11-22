using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkideya.Controllers;
using Orkideya.Data;
using Orkideya.Models;

internal static class AdminDeleteScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        await using var db = open();
        var category = new Category { Name = "Synthetic delete category" };
        var free = new Product { Name = "Synthetic free product", Category = category };
        free.ProductVariants.Add(new ProductVariant { Product = null!, Size = "A", Price = 10 });
        free.ProductVariants.Add(new ProductVariant { Product = null!, Size = "B", Price = 12 });
        var ordered = new Product { Name = "Synthetic ordered product", Category = category };
        ordered.ProductVariants.Add(new ProductVariant { Product = null!, Size = "A", Price = 10 });
        var order = new Order
        {
            CustomerName = "Synthetic delete", WhatsAppNumber = "0000000000", ShippingAddress = "Synthetic",
            City = "Synthetic", OrderDate = DateTime.UtcNow, TotalAmount = 10, ShippingCost = 0, Status = "Pending"
        };
        db.AddRange(category, free, ordered, order);
        await db.SaveChangesAsync();
        db.OrderItems.Add(new OrderItem { OrderId = order.OrderId, ProductId = ordered.ProductId, VariantSize = "A", Quantity = 1, UnitPrice = 10 });
        await db.SaveChangesAsync();

        // ProductEditor is not used by the delete actions.
        var products = new AdminProductsController(db, null!);
        var categories = new AdminCategoriesController(db);

        check(await products.Delete(ordered.ProductId, default) is ConflictObjectResult, "Product with order items cannot be deleted");
        check(await db.Products.AnyAsync(p => p.ProductId == ordered.ProductId), "Ordered product still exists after refusal");
        check(await products.DeleteVariant(ordered.ProductId, ordered.ProductVariants.First().ProductVariantId, default) is ConflictObjectResult,
            "Last variant cannot be deleted");
        check(await categories.Delete(category.CategoryId, default) is ConflictObjectResult, "Category in use cannot be deleted");

        var first = free.ProductVariants.First().ProductVariantId;
        check(await products.DeleteVariant(free.ProductId, first, default) is NoContentResult, "Non-last variant is deleted");
        check(await products.Delete(free.ProductId, default) is NoContentResult, "Product without orders is deleted");
        check(!await db.Products.AnyAsync(p => p.ProductId == free.ProductId) &&
              !await db.ProductVariants.AnyAsync(v => v.ProductId == free.ProductId), "Deleted product leaves no variants");
        check(await categories.Delete(category.CategoryId, default) is ConflictObjectResult, "Category still in use by ordered product");
    }
}

