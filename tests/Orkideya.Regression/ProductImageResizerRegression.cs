using Microsoft.Extensions.Logging.Abstractions;
using Orkideya.Services;
using SkiaSharp;

internal static class ProductImageResizerRegression
{
    public static void Run(Action<bool, string> check)
    {
        var dir = Path.Combine(Path.GetTempPath(), "orkidea-resize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var resizer = new ProductImageResizer(NullLogger<ProductImageResizer>.Instance);

            var big = Path.Combine(dir, "big.png");
            File.WriteAllBytes(big, MakePng(1600, 1000));
            check(resizer.TryCreateVariants(big), "Resizer reports success for large image");
            foreach (var (width, height) in new[] { (400, 250), (800, 500), (1200, 750) })
            {
                var (w, h) = Size(ProductImageResizer.VariantPath(big, width));
                check(w == width && h == height, $"Large image variant {width} has expected size");
            }
            check(File.Exists(big), "Original kept");

            var small = Path.Combine(dir, "small.png");
            File.WriteAllBytes(small, MakePng(300, 200));
            check(resizer.TryCreateVariants(small), "Resizer reports success for small image");
            foreach (var width in ProductImageResizer.Widths)
            {
                var (w, h) = Size(ProductImageResizer.VariantPath(small, width));
                check(w == 300 && h == 200, $"Small image variant {width} is not upscaled");
            }

            var corrupt = Path.Combine(dir, "corrupt.jpg");
            File.WriteAllBytes(corrupt, new byte[] { 1, 2, 3, 4, 5 });
            bool ok;
            try { ok = resizer.TryCreateVariants(corrupt); }
            catch { check(false, "Corrupt image threw"); return; }
            check(!ok && !ProductImageResizer.HasAllVariants(corrupt), "Corrupt image does not throw and writes nothing");

            ProductImageResizer.DeleteVariants(big);
            check(!File.Exists(ProductImageResizer.VariantPath(big, 400)), "Variants deleted");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static byte[] MakePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Purple };
            canvas.DrawCircle(width / 2f, height / 2f, height / 3f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static (int, int) Size(string path)
    {
        using var bitmap = SKBitmap.Decode(path);
        return bitmap is null ? (0, 0) : (bitmap.Width, bitmap.Height);
    }
}
