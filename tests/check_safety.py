"""Source/package boundary checks. These do not replace ASP.NET runtime tests."""
from pathlib import Path
import xml.etree.ElementTree as ET
r=Path(__file__).resolve().parents[1]/'Orkideya'
def read(f): return (r/f).read_text(encoding='utf-8-sig')
checks={
 'no public customer workbook':not any((r/'wwwroot').rglob('*.xlsx')),
 'global MVC antiforgery filter':'options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())' in read('Program.cs'),
 'initialization requires explicit flag':'GetValue<bool>("Initialization:Run")' in read('Program.cs'),
 'shipping seed does not delete rates':'RemoveRange' not in read('Data/DbInitializer.cs'),
 'bootstrap password comes from configuration':'configuration["Bootstrap:AdminPassword"]' in read('Data/DbInitializer.cs') and 'string adminPassword =' not in read('Data/DbInitializer.cs'),
 'checkout no longer writes public spreadsheet':'AddOrderToSheet' not in read('Controllers/CheckoutController.cs'),
 'export service has no filesystem persistence':'SaveAs(_filePath)' not in read('Services/ExcelExportService.cs') and 'IWebHostEnvironment' not in read('Services/ExcelExportService.cs'),
 'confirmation page does not generate tokens':'GenerateEmailConfirmationTokenAsync' not in read('Areas/Identity/Pages/Account/RegisterConfirmation.cshtml.cs'),
 'login enables lockout':'lockoutOnFailure: true' in read('Areas/Identity/Pages/Account/Login.cshtml.cs'),
 'private exports excluded from content':any('App_Data/**' in n.attrib.get('Remove','') for n in ET.parse(r/'Orkideya.csproj').iter('Content')),
 'product list encodes description':'Html.Raw(item.Description' not in read('Areas/Admin/Views/Products/Index.cshtml'),
}
import re
controller=read('Controllers/CartController.cs')
view=read('Views/Cart/Index.cshtml')
for action in ['RemoveFromCart','IncreaseQuantity','DecreaseQuantity']:
 checks[action+' only accepts protected POST']=bool(re.search(r'\[HttpPost\]\s*\[ValidateAntiForgeryToken\]\s*public IActionResult '+action,controller))
 checks[action+' rendered as POST form']=bool(re.search(r'<form[^>]*asp-action="'+action+r'"[^>]*method="post"',view))
checks['cart does not promise free shipping']='مجاني' not in view
checkout=read('Controllers/CheckoutController.cs')
checkout_view=read('Views/Checkout/Index.cshtml')
checks['checkout binds validated request DTO']='[Bind(Prefix = "Input")] CheckoutInput input' in checkout
checks['checkout rejects missing or negative shipping']='rate == null || rate.Price < 0' in checkout
checks['checkout refreshes prices before creating order']=checkout.index('RefreshPrices(cart, variants, out var priceChanged)') < checkout.index('var order = new Order')
checks['checkout stops on changed prices']='if (priceChanged)' in checkout and 'if (!ModelState.IsValid)' in checkout
checks['checkout renders server errors']='asp-validation-summary="All"' in checkout_view
checks['checkout preserves typed inputs']='asp-for="Input.FirstName"' in checkout_view and 'asp-for="Input.ShippingAddress"' in checkout_view
checks['checkout does not offer unpriced other city']='city-item-other' not in checkout_view and 'otherCity' not in checkout
products=read('Areas/Admin/Controllers/ProductsController.cs')
images=read('Services/ProductImageStorage.cs')
checks['products use dedicated request model']='Create(ProductInput input' in products and 'Edit(int id, ProductInput input' in products
checks['product validation precedes upload']='ModelState.IsValid && await TrySaveImageAsync(input, imageFile)' in products
checks['product controller does not suppress category errors']='ModelState.Remove' not in products[:products.index('public async Task<IActionResult> AddVariant')]
checks['image upload has declared and streamed byte limits']='file.Length > MaxBytes' in images and 'output.Length + count > MaxBytes' in images
checks['image storage generates filename']='Guid.NewGuid().ToString("N") + image.Extension' in images
checks['image storage checks signatures']='SequenceEqual' in images and 'محتوى الملف لا يطابق' in images
checks['product requests cap multipart payload']='RequestSizeLimit(6 * 1024 * 1024)' in products and 'RequestFormLimits' in products
checks['both product forms show upload errors']=all('asp-validation-summary="All"' in read('Areas/Admin/Views/Products/'+name+'.cshtml') for name in ['Create','Edit'])
variants=read('Areas/Admin/Controllers/ProductVariantsController.cs')
reviews=read('Areas/Admin/Controllers/ReviewsController.cs')
checks['variant details load variant entity']='v => v.ProductVariantId == id' in variants and 'View(variant)' in variants
checks['variant deletion guards missing row']='if (variant == null) return NotFound();' in variants[variants.index('public async Task<IActionResult> DeleteConfirmed'):]
checks['variant forms use dedicated DTO']=all('Orkideya.Models.ProductVariantInput' in read('Areas/Admin/Views/ProductVariants/'+name+'.cshtml') for name in ['Create','Edit'])
checks['inline variant creation uses same request DTO']='AddVariant(ProductVariantInput input)' in products
checks['review create and edit bind submitted Rating']=reviews.count('Id,CustomerName,Message,Rating,IsVisible') == 2
checks['new review is hidden by default']='Message = "", IsVisible = false' in reviews
checks['review views use shared rating script']=all('~/js/review-rating.js' in read('Areas/Admin/Views/Reviews/'+name+'.cshtml') for name in ['Create','Edit'])
notifications=read('Services/WhatsAppNotificationService.cs')
checks['WhatsApp requires explicit opt-in']='if (!_configuration.GetValue<bool>("Notifications:WhatsAppEnabled")) return;' in notifications
checks['WhatsApp recipient comes from configuration']='_configuration["Twilio:AdminNumber"]' in notifications
checks['WhatsApp does not print exception payload']='Console.WriteLine' not in notifications and 'exception.Message' not in notifications
for name,passed in checks.items():print(('PASS' if passed else 'FAIL')+': '+name)
raise SystemExit(0 if all(checks.values()) else 1)
