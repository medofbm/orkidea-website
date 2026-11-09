using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;

namespace Orkideya.Application.Products;

public sealed class ProductEditor(ApplicationDbContext db, ProductImageStorage images)
{
    public async Task ValidateInputAsync(ProductInput input, ModelStateDictionary modelState, CancellationToken ct)
    {
        if (!ProductImageStorage.IsAllowedReference(input.ImageUrl))
            modelState.AddModelError(nameof(input.ImageUrl), "استخدم رابط HTTPS أو صورة محلية ضمن /images/.");
        if (input.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.CategoryId == input.CategoryId.Value, ct))
            modelState.AddModelError(nameof(input.CategoryId), "الفئة المختارة غير موجودة.");
    }

    public static string? SerializeGallery(List<string> images) =>
        images.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(images);

    public async Task<List<string>?> SaveGalleryAsync(
        ProductInput input, List<string> retained, List<IFormFile>? uploads, IFormFile? cover, ModelStateDictionary modelState, CancellationToken cancellationToken)
    {
        if (!modelState.IsValid) return null;
        uploads ??= [];
        if (retained.Count + uploads.Count > 5)
        {
            modelState.AddModelError("galleryFiles", "يمكن إضافة خمس صور إضافية كحد أقصى.");
            return null;
        }

        if (uploads.Sum(file => file.Length) + (cover?.Length ?? 0) > ProductImageStorage.MaxBytes)
        {
            modelState.AddModelError("galleryFiles", "إجمالي حجم الصور المرفوعة لا يتجاوز 5 ميجابايت.");
            return null;
        }

        if (uploads.Count == 0 && cover is null) return retained;

        try
        {
            var gallery = new List<(byte[] Bytes, string Extension)>();
            foreach (var file in uploads)
                gallery.Add(await ProductImageStorage.ReadValidatedAsync(file, cancellationToken));
            if (cover is not null)
                input.ImageUrl = await images.SaveAsync(cover, cancellationToken);
            foreach (var image in gallery)
                retained.Add(await images.SaveValidatedAsync(image, cancellationToken));
            return retained;
        }
        catch (InvalidDataException exception)
        {
            modelState.AddModelError("galleryFiles", exception.Message);
            return null;
        }
    }
}
