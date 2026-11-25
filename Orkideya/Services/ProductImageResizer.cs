using SkiaSharp;

namespace Orkideya.Services;

/// <summary>
/// Writes WebP width variants (-400, -800, -1200) next to a product image original.
/// Never upscales: a smaller original is re-encoded at its own width under every variant name,
/// so the set is always complete. The TryCreate methods never throw.
/// </summary>
public sealed class ProductImageResizer(ILogger<ProductImageResizer> logger)
{
    public static readonly int[] Widths = [400, 800, 1200];
    public const int Quality = 80;
    public const int MaxDimension = 8000;       // longest side in pixels
    public const long MaxPixels = 40_000_000;   // about 160 MB decoded at 4 bytes per pixel

    public static bool IsWithinLimits(int width, int height) =>
        width > 0 && height > 0 && width <= MaxDimension && height <= MaxDimension &&
        (long)width * height <= MaxPixels;

    /// <summary>
    /// Reads only the image header (no pixel allocation) and checks the declared size.
    /// Returns true when the header cannot be read at all (nothing to allocate; the decoder rejects it later).
    /// </summary>
    public static bool HeaderWithinLimits(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        return codec is null || IsWithinLimits(codec.Info.Width, codec.Info.Height);
    }

    public static string VariantPath(string originalPath, int width) =>
        Path.Combine(Path.GetDirectoryName(originalPath) ?? "",
            Path.GetFileNameWithoutExtension(originalPath) + "-" + width + ".webp");

    public static bool HasAllVariants(string originalPath) =>
        Widths.All(width => File.Exists(VariantPath(originalPath, width)));

    public static void DeleteVariants(string originalPath)
    {
        foreach (var width in Widths)
        {
            try { File.Delete(VariantPath(originalPath, width)); } catch { /* best effort */ }
        }
    }

    /// <summary>Returns true when all variants were written. Never throws.</summary>
    public bool TryCreateVariants(string originalPath)
    {
        try { return CreateVariants(File.ReadAllBytes(originalPath), originalPath); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image variants could not be created for {File}", Path.GetFileName(originalPath));
            return false;
        }
    }

    /// <summary>Same, from bytes already in memory (the original path only names the output files).</summary>
    public bool TryCreateVariants(byte[] bytes, string originalPath)
    {
        try { return CreateVariants(bytes, originalPath); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image variants could not be created for {File}", Path.GetFileName(originalPath));
            return false;
        }
    }

    private bool CreateVariants(byte[] bytes, string originalPath)
    {
        if (!HeaderWithinLimits(bytes))
        {
            logger.LogWarning("Image {File} is larger than {Max} px per side or {MaxPixels} pixels; variants skipped",
                Path.GetFileName(originalPath), MaxDimension, MaxPixels);
            return false;
        }
        using var source = DecodeOriented(bytes);
        if (source is null || source.Width <= 0 || source.Height <= 0)
        {
            logger.LogWarning("Image {File} could not be decoded; variants skipped", Path.GetFileName(originalPath));
            return false;
        }
        var written = new List<string>();
        try
        {
            foreach (var width in Widths)
            {
                var targetWidth = Math.Min(width, source.Width);
                var targetHeight = Math.Max(1, (int)Math.Round(source.Height * (double)targetWidth / source.Width));
                using var scaled = targetWidth == source.Width
                    ? source.Copy()
                    : source.Resize(new SKImageInfo(targetWidth, targetHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (scaled is null) throw new InvalidOperationException("Resize failed");
                using var image = SKImage.FromBitmap(scaled);
                using var data = image.Encode(SKEncodedImageFormat.Webp, Quality)
                    ?? throw new InvalidOperationException("WebP encode failed");
                var path = VariantPath(originalPath, width);
                var temp = path + ".tmp";
                using (var stream = File.Create(temp)) data.SaveTo(stream);
                File.Move(temp, path, overwrite: true);
                written.Add(path);
            }
            return true;
        }
        catch
        {
            foreach (var path in written) { try { File.Delete(path); } catch { } }
            throw;
        }
    }

    private static SKBitmap? DecodeOriented(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        if (codec is null) return null;
        if (!IsWithinLimits(codec.Info.Width, codec.Info.Height)) return null; // checked before any pixel allocation
        // Decode straight into the platform colour type so grayscale, gray+alpha and indexed PNGs can be encoded as WebP.
        // Like browsers, accept images whose data ends early (IncompleteInput) instead of rejecting them.
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        if (info.Width <= 0 || info.Height <= 0) return null;
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            return null;
        }
        var rotation = codec.EncodedOrigin switch
        {
            SKEncodedOrigin.RightTop => 90,
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0
        };
        if (rotation == 0) return bitmap;
        var swap = rotation != 180;
        var rotated = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
            canvas.RotateDegrees(rotation);
            canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
            canvas.DrawBitmap(bitmap, 0, 0);
        }
        bitmap.Dispose();
        return rotated;
    }
}
