using Microsoft.Extensions.Configuration;
using ClosedXML.Excel;
using Orkideya.Models;
using Orkideya.Services;

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS: " + name);
}
var service = new ExcelExportService();
CartSummaryRegression.Run(Check);
var order = new Order {
    OrderId = 123, CustomerName = "=1+1", WhatsAppNumber = "+218000000000",
    ShippingAddress = "Synthetic test address", City = "Tripoli", ShippingCost = 10m,
    TotalAmount = 42.50m, Status = "Pending", OrderDate = new DateTime(2026, 1, 1)
};
using (var stream = new MemoryStream(service.ExportOrders(new[] {order})))
using (var workbook = new XLWorkbook(stream))
{
    var sheet = workbook.Worksheet(1);
    Check(sheet.LastRowUsed()!.RowNumber() == 2, "One row per order");
    Check(sheet.Cell(2, 7).GetValue<decimal>() == 42.50m, "Total preserved exactly");
    Check(sheet.Cell(2, 8).GetValue<decimal>() == 10m, "Shipping preserved separately");
    Check(!sheet.Cell(2, 2).HasFormula, "Customer input stays text");
    Check(sheet.Cell(2, 2).GetString() == "=1+1", "Customer text preserved");
    Check(sheet.RightToLeft, "Arabic worksheet direction");
}
using (var stream = new MemoryStream(service.ExportOrders(Array.Empty<Order>())))
using (var workbook = new XLWorkbook(stream))
    Check(workbook.Worksheet(1).LastRowUsed()!.RowNumber() == 1, "Empty export contains headers only");

Check(Orkideya.Application.Cart.CartLimits.CanAdd(0, 1), "First item is allowed");
Check(Orkideya.Application.Cart.CartLimits.CanAdd(98, 1), "Quantity can reach the cap");
Check(!Orkideya.Application.Cart.CartLimits.CanAdd(99, 1), "Cannot exceed the cap");
Check(!Orkideya.Application.Cart.CartLimits.CanAdd(0, 0), "Zero quantity rejected");
Check(!Orkideya.Application.Cart.CartLimits.CanAdd(0, -1), "Negative quantity rejected");
Check(!Orkideya.Application.Cart.CartLimits.CanAdd(1, int.MaxValue), "Integer overflow attempt rejected");
Check(!Orkideya.Application.Cart.CartLimits.CanAdd(-1, 1), "Invalid session quantity rejected");

static bool ValidCheckout(CheckoutInput input) =>
    System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input,
        new System.ComponentModel.DataAnnotations.ValidationContext(input),
        new List<System.ComponentModel.DataAnnotations.ValidationResult>(), true);
var request = new CheckoutInput { WhatsAppNumber = "+218910000000", FirstName = "Test",
    LastName = "Customer", ShippingAddress = "Synthetic address", ShippingRateId = 1 };
Check(ValidCheckout(request), "Valid synthetic checkout accepted");
request.ShippingRateId = 0;
Check(!ValidCheckout(request), "Unpriced city rejected");
request.ShippingRateId = -1;
Check(!ValidCheckout(request), "Negative city rejected");
request.ShippingRateId = 1; request.FirstName = new string('a', 51);
Check(!ValidCheckout(request), "Overlong name rejected");
request.FirstName = "Test"; request.ShippingAddress = " ";
Check(!ValidCheckout(request), "Blank address rejected");
request.ShippingAddress = "Synthetic address"; request.WhatsAppNumber = "abc";
Check(!ValidCheckout(request), "Non-phone value rejected");

request.WhatsAppNumber = "1------";
Check(!ValidCheckout(request), "Punctuation cannot replace phone digits");

Check(ProductImageStorage.IsAllowedReference("/images/product.png"), "Local image reference allowed");
Check(ProductImageStorage.IsAllowedReference("https://example.invalid/product.jpg"), "HTTPS image reference allowed");
Check(!ProductImageStorage.IsAllowedReference("javascript:alert(1)"), "Script image URL rejected");
Check(!ProductImageStorage.IsAllowedReference("//example.invalid/image.jpg"), "Protocol-relative image URL rejected");
Check(!ProductImageStorage.IsAllowedReference("/images/../private/file"), "Relative traversal rejected");
Check(!ProductImageStorage.IsAllowedReference("https://user:password@example.invalid/image.jpg"), "Image URL credentials rejected");
Check(!ProductImageStorage.IsAllowedReference("http://example.invalid/image.jpg"), "Plain HTTP image URL rejected");

static Microsoft.AspNetCore.Http.FormFile Upload(byte[] bytes, string name) =>
    new(new MemoryStream(bytes), 0, bytes.Length, "imageFile", name);
static async Task RejectUpload(byte[] bytes, string name, string message)
{
    try { await ProductImageStorage.ReadValidatedAsync(Upload(bytes, name)); }
    catch (InvalidDataException) { Check(true, message); return; }
    throw new InvalidOperationException(message);
}
await RejectUpload(Array.Empty<byte>(), "empty.png", "Empty upload rejected");
await RejectUpload(new byte[] {1,2,3,4}, "fake.png", "Fake PNG rejected");
await RejectUpload(new byte[] {1,2,3,4}, "fake.jpg", "Fake JPEG rejected");
await RejectUpload(new byte[] {1,2,3,4}, "payload.svg", "SVG rejected");
await RejectUpload(new byte[ProductImageStorage.MaxBytes + 1], "large.png", "Oversized upload rejected");
var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jvXkAAAAASUVORK5CYII=");
var imageResult = await ProductImageStorage.ReadValidatedAsync(Upload(pngBytes, "../../untrusted.png"));
Check(imageResult.Extension == ".png", "Untrusted filename reduced to allowed extension");
Check(imageResult.Bytes.SequenceEqual(pngBytes), "PNG upload bytes preserved");
await RejectUpload(pngBytes, "mismatch.jpg", "Extension/signature mismatch rejected");

static bool ValidVariant(ProductVariantInput input) =>
    System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input,
        new System.ComponentModel.DataAnnotations.ValidationContext(input),
        new List<System.ComponentModel.DataAnnotations.ValidationResult>(), true);
var variantInput = new ProductVariantInput { ProductId = 1, Size = "50 ml", Price = 30.50m };
Check(ValidVariant(variantInput), "Valid variant request accepted without navigation entity");
variantInput.Price = 0;
Check(!ValidVariant(variantInput), "Zero variant price rejected");
variantInput.Price = -1;
Check(!ValidVariant(variantInput), "Negative variant price rejected");
variantInput.Price = 1.001m;
Check(!ValidVariant(variantInput), "Fractional cents rejected");
variantInput.Price = 30; variantInput.Size = " ";
Check(!ValidVariant(variantInput), "Blank variant size rejected");
variantInput.Size = new string('x', 51);
Check(!ValidVariant(variantInput), "Overlong variant size rejected");
variantInput.Size = "50 ml"; variantInput.ProductId = 0;
Check(!ValidVariant(variantInput), "Nonpositive product reference rejected");

var notificationLogger = new RecordingLogger();
var disabledNotifications = new WhatsAppNotificationService(new ConfigurationBuilder().Build(), notificationLogger);
await disabledNotifications.SendNotification(order);
Check(notificationLogger.Count == 0, "Default notification configuration skips real send without logging provider data");
var enabledConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Notifications:WhatsAppEnabled"] = "true" }).Build();
await new WhatsAppNotificationService(enabledConfiguration, notificationLogger).SendNotification(order);
Check(notificationLogger.Count == 1, "Enabled but incomplete notification configuration returns before provider call");
