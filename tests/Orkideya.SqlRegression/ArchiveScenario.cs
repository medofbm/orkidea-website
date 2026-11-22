using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Orkideya.Application.Orders;
using Orkideya.Controllers;
using Orkideya.Data;
using Orkideya.Models;

/// <summary>
/// Phase 4B: archived products/variants/categories, receipt reviews and the migration seed.
/// Runs on a dedicated generated test database only (never a real one).
/// </summary>
internal static class ArchiveScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        await using var db = open();

        // Migration seed: the five Facebook comments are present, visible, anonymous and rated 5.
        var seeded = await db.Reviews.AsNoTracking().Where(review => review.Source == "facebook").ToListAsync();
        check(seeded.Count == 5 && seeded.All(review => review.IsVisible && !review.AllowName && review.CustomerName == "" &&
            review.Rating == 5 && review.OrderId == null && review.CreatedAtUtc == new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc)),
            "Migration seeds exactly five visible anonymous Facebook reviews");
        check(seeded.Any(review => review.Message == "ربي يعاونك ويفتحها عليك ❤️"), "Seeded Facebook review keeps its emoji text");

        // Fixtures: one visible product with two sizes, one product with one size, one archived category.
        var category = new Category { Name = "Synthetic archive category" };
        var archivedCategory = new Category { Name = "Synthetic archived category" };
        var shown = new Product { Name = "Synthetic archive shown", Category = category };
        shown.ProductVariants.Add(new ProductVariant { Product = null!, Size = "A", Price = 10 });
        shown.ProductVariants.Add(new ProductVariant { Product = null!, Size = "B", Price = 12 });
        var hidden = new Product { Name = "Synthetic archive hidden", Category = category };
        hidden.ProductVariants.Add(new ProductVariant { Product = null!, Size = "A", Price = 10 });
        var single = new Product { Name = "Synthetic archive single", Category = category };
        single.ProductVariants.Add(new ProductVariant { Product = null!, Size = "A", Price = 10 });
        var inArchivedCategory = new Product { Name = "Synthetic archive in archived category", Category = archivedCategory };
        inArchivedCategory.ProductVariants.Add(new ProductVariant { Product = null!, Size = "A", Price = 10 });
        db.AddRange(category, archivedCategory, shown, hidden, single, inArchivedCategory);
        await db.SaveChangesAsync();

        var products = new AdminProductsController(db, null!);
        var categories = new AdminCategoriesController(db);
        var catalogue = new CatalogueController(db);

        // Archive the product: hidden from list and detail, still in admin with a flag.
        check(await products.Archive(hidden.ProductId, new(true), default) is OkObjectResult, "Admin can archive a product");
        var list = Json(((OkObjectResult)await catalogue.Products(1, default, category.CategoryId)).Value);
        var listedIds = list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToList();
        check(!listedIds.Contains(hidden.ProductId) && listedIds.Contains(shown.ProductId), "Archived product is hidden from the catalogue list");
        check(await catalogue.Product(hidden.ProductId, default) is NotFoundObjectResult, "Archived product detail returns 404");
        var adminList = Json(((OkObjectResult)await products.Index(default, "Synthetic archive hidden")).Value);
        check(adminList.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("ProductId").GetInt32() == hidden.ProductId &&
            item.GetProperty("IsArchived").GetBoolean()), "Admin product list still shows the archived product with a flag");
        var adminDetail = Json(((OkObjectResult)await products.Details(hidden.ProductId, default)).Value);
        check(adminDetail.GetProperty("IsArchived").GetBoolean(), "Admin product detail includes isArchived");

        // Cart refuses archived products and archived sizes.
        var session = new ArchiveSession();
        var cart = new CartController(db)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } } };
        var hiddenVariant = hidden.ProductVariants.First().ProductVariantId;
        check(!Success(await cart.AddToCartAjax(hiddenVariant, 1)), "Archived product cannot be added to the cart");
        var shownA = shown.ProductVariants.First(variant => variant.Size == "A").ProductVariantId;
        var shownB = shown.ProductVariants.First(variant => variant.Size == "B").ProductVariantId;
        check(Success(await cart.AddToCartAjax(shownA, 1)), "Visible size can be added to the cart");

        // Archive size A: hidden from detail, refused by the cart; size B stays; archiving the last visible size is refused.
        check(await products.ArchiveVariant(shown.ProductId, shownA, new(true), default) is OkObjectResult, "Admin can archive one of two sizes");
        var detail = Json(((OkObjectResult)await catalogue.Product(shown.ProductId, default)).Value);
        check(detail.GetProperty("variants").GetArrayLength() == 1 && detail.GetProperty("variants")[0].GetProperty("id").GetInt32() == shownB,
            "Archived size is hidden from product detail");
        check(!Success(await cart.AddToCartAjax(shownA, 1)), "Archived size cannot be added to the cart");
        check(await products.ArchiveVariant(shown.ProductId, shownB, new(true), default) is ConflictObjectResult,
            "Archiving the last visible size returns 409");
        check(await products.ArchiveVariant(single.ProductId, single.ProductVariants.First().ProductVariantId, new(true), default) is ConflictObjectResult,
            "Archiving the only size of a visible product returns 409");
        check(await products.ArchiveVariant(shown.ProductId, 2147483647, new(true), default) is NotFoundResult, "Archiving an unknown size returns 404");
        check(await products.ArchiveVariant(shown.ProductId, shownA, new(false), default) is OkObjectResult, "Archived size can be restored");

        // The cart view marks an archived size as unavailable (cart loaded from the session before archiving).
        check(await products.ArchiveVariant(shown.ProductId, shownB, new(true), default) is OkObjectResult, "Size B can be archived once A is visible again");
        var cartView = Json(((JsonResult)await cart.ApiCart(default)).Value);
        check(cartView.GetProperty("items").EnumerateArray().Single().GetProperty("available").GetBoolean(),
            "Cart still shows the visible size as available");
        check(await products.ArchiveVariant(shown.ProductId, shownB, new(false), default) is OkObjectResult, "Size B restored");
        check(await products.ArchiveVariant(shown.ProductId, shownA, new(true), default) is OkObjectResult, "Size A archived while it is in the cart");
        cartView = Json(((JsonResult)await cart.ApiCart(default)).Value);
        check(!cartView.GetProperty("items").EnumerateArray().Single().GetProperty("available").GetBoolean(),
            "Cart marks an archived size as no longer available");

        // Checkout re-validation rejects the archived line with the Arabic message.
        var checkout = new CheckoutController(db, new OrderService(db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } } };
        checkout.TempData = new TempDataDictionary(checkout.HttpContext, new NoTempData());
        var checkoutResult = await checkout.ApiCheckout();
        check(checkoutResult is ConflictObjectResult conflict &&
            Json(conflict.Value).GetProperty("message").GetString() == "بعض المنتجات في سلتك لم تعد متاحة. راجعي السلة.",
            "Checkout rejects archived cart lines with the Arabic availability message");
        check(await products.ArchiveVariant(shown.ProductId, shownA, new(false), default) is OkObjectResult, "Size A restored");

        // Categories: archived ones disappear from the storefront list, products stay.
        check(await categories.Archive(archivedCategory.CategoryId, new(true), default) is OkObjectResult, "Admin can archive a category");
        var storefront = Json(((OkObjectResult)await catalogue.Products(1, default)).Value);
        var storefrontCategories = storefront.GetProperty("categories").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToList();
        check(!storefrontCategories.Contains(archivedCategory.CategoryId) && storefrontCategories.Contains(category.CategoryId),
            "Archived category is hidden from the storefront category list");
        var adminCategories = Json(((OkObjectResult)await categories.Index(default, "Synthetic archived")).Value);
        check(adminCategories.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("CategoryId").GetInt32() == archivedCategory.CategoryId &&
            item.GetProperty("IsArchived").GetBoolean()), "Admin category list shows the archived flag");
        check(await categories.Archive(2147483647, new(true), default) is NotFoundObjectResult, "Archiving an unknown category returns 404");

        // Restoring brings the product back.
        check(await products.Archive(hidden.ProductId, new(false), default) is OkObjectResult &&
            await catalogue.Product(hidden.ProductId, default) is OkObjectResult, "Restored product is visible again");

        // Receipt review: once per order, saved hidden, first name only.
        var token = Guid.NewGuid();
        var order = new Order
        {
            CheckoutToken = token, CheckoutRequestHash = new string('C', 64), CustomerName = "Synthetic Reviewer",
            WhatsAppNumber = "+218000000000", ShippingAddress = "Synthetic", City = "Tripoli", ShippingCost = 10, TotalAmount = 20,
            OrderDate = DateTime.UtcNow, Status = "Pending"
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var receiptSession = new ArchiveSession();
        receiptSession.SetString("CompletedCheckoutToken", token.ToString("D"));
        var receiptController = new CheckoutController(db, new OrderService(db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = receiptSession } } };
        var strangerController = new CheckoutController(db, new OrderService(db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = new ArchiveSession() } } };
        check(await strangerController.ApiReceiptReview(new(5, "x", true)) is NotFoundResult, "Receipt review without a receipt session returns 404");
        check(await receiptController.ApiReceiptReview(new(0, null, false)) is BadRequestObjectResult, "Receipt review with invalid rating returns 400");
        check(await receiptController.ApiReceiptReview(new(4, "تجربة جميلة", true)) is AcceptedResult, "Receipt review is accepted once");
        check(await receiptController.ApiReceiptReview(new(5, "مرة ثانية", true)) is ConflictObjectResult conflictReview &&
            Json(conflictReview.Value).GetProperty("message").GetString() == "قيّمتي هذا الطلب قبل.", "Second receipt review for the same order returns 409");
        var saved = await db.Reviews.AsNoTracking().SingleAsync(review => review.OrderId == order.OrderId);
        check(!saved.IsVisible && saved.AllowName && saved.Source == "site" && saved.Rating == 4 && saved.CustomerName == "Synthetic",
            "Receipt review is saved hidden with the first name only");
        db.Reviews.Add(new Review { CustomerName = "X", Message = "duplicate", OrderId = order.OrderId, IsVisible = false });
        var unique = false;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { unique = true; }
        check(unique, "SQL unique filtered index allows only one review per order");
        db.ChangeTracker.Clear();
        db.Reviews.Add(new Review { CustomerName = "X", Message = "no order a", IsVisible = false });
        db.Reviews.Add(new Review { CustomerName = "X", Message = "no order b", IsVisible = false });
        await db.SaveChangesAsync();
        check(true, "Many reviews without an order are allowed (filtered index)");

        // Public list: approved review shows the first word of the name only when allowed.
        await db.Reviews.Where(review => review.Id == saved.Id).ExecuteUpdateAsync(setters => setters.SetProperty(review => review.IsVisible, true));
        var latest = Json(((OkObjectResult)await new ReviewsApiController(db).Latest(default)).Value);
        var first = latest.GetProperty("items").EnumerateArray().First(item => item.GetProperty("id").GetInt32() == saved.Id);
        check(first.GetProperty("name").GetString() == "Synthetic" && first.GetProperty("source").GetString() == "site",
            "Public reviews show source and the first name when the customer allowed it");
        await db.Reviews.Where(review => review.Id == saved.Id).ExecuteUpdateAsync(setters => setters.SetProperty(review => review.AllowName, false));
        latest = Json(((OkObjectResult)await new ReviewsApiController(db).Latest(default)).Value);
        check(latest.GetProperty("items").EnumerateArray().First(item => item.GetProperty("id").GetInt32() == saved.Id)
            .GetProperty("name").ValueKind == JsonValueKind.Null, "Public reviews hide the name without consent");

        // Deleting an order never deletes its review: the link is cleared instead.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Orders WHERE OrderId = {0}", order.OrderId);
        var survivor = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(review => review.Id == saved.Id);
        check(survivor is not null && survivor.OrderId == null, "Deleting an order keeps its review and clears the link");

        // Contact messages read state.
        var message = new ContactMessage { Name = "Synthetic", Email = "", PhoneNumber = "0000000000", Message = "synthetic message" };
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync();
        var feedback = new AdminFeedbackController(db);
        check(!message.IsRead, "New contact message starts unread");
        check(await feedback.MarkRead(message.Id, new(true), default) is OkObjectResult, "Admin can mark a contact message read");
        check((await db.ContactMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id)).IsRead, "Read state is stored");
        var messages = Json(((OkObjectResult)await feedback.ContactMessages(default)).Value);
        check(messages.GetProperty("items").EnumerateArray().First(item => item.GetProperty("id").GetInt32() == message.Id)
            .GetProperty("isRead").GetBoolean(), "Admin contact list includes isRead");
        check(await feedback.MarkRead(2147483647, new(true), default) is NotFoundResult, "Marking an unknown message returns 404");
        var reviewsList = Json(((OkObjectResult)await feedback.Reviews(default)).Value);
        check(reviewsList.GetProperty("items").EnumerateArray().All(item => item.TryGetProperty("orderId", out _) &&
            item.TryGetProperty("createdAtUtc", out _) && item.TryGetProperty("allowName", out _)) &&
            reviewsList.GetProperty("items").EnumerateArray().Count(item => item.GetProperty("source").GetString() == "facebook") == 5,
            "Admin review list includes source, allowName, date and order link");
    }

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);

    private static bool Success(JsonResult result) => Json(result.Value).GetProperty("success").GetBoolean();

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class ArchiveSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;
        public string Id => "synthetic-archive-session";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
