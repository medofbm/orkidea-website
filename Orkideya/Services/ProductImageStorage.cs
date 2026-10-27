namespace Orkideya.Services;

/// <summary>Bounded upload and file-type checks, not a malware scanner or full image decoder.</summary>
public sealed class ProductImageStorage
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private readonly string _directory;
    public ProductImageStorage(IWebHostEnvironment environment)
    {
        _directory = Path.Combine(environment.WebRootPath, "images", "products");
    }

    public static bool IsAllowedReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Any(char.IsControl) || value.Contains('\\')) return false;
        if (value.StartsWith("/images/", StringComparison.Ordinal))
            return !value.Contains("..") && !value.Contains('?') && !value.Contains('#');
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host) &&
            string.IsNullOrEmpty(uri.UserInfo);
    }

    public static async Task<(byte[] Bytes, string Extension)> ReadValidatedAsync(
        IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0 || file.Length > MaxBytes)
            throw new InvalidDataException("حجم الصورة يجب أن يكون أكبر من صفر وألا يتجاوز 5 ميجابايت.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
            throw new InvalidDataException("الصور المسموحة PNG وJPEG فقط.");
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            if (output.Length + count > MaxBytes)
                throw new InvalidDataException("الصورة أكبر من الحد المسموح.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        var bytes = output.ToArray();
        if (bytes.LongLength != file.Length)
            throw new InvalidDataException("تعذر التحقق من حجم الصورة.");
        var png = bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) &&
            bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8);
        var jpeg = bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 &&
            bytes[2] == 0xff && bytes[^2] == 0xff && bytes[^1] == 0xd9;
        if ((extension == ".png" && !png) || (extension != ".png" && !jpeg))
            throw new InvalidDataException("محتوى الملف لا يطابق نوع الصورة.");
        return (bytes, extension == ".jpeg" ? ".jpg" : extension);
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var image = await ReadValidatedAsync(file, cancellationToken);
        Directory.CreateDirectory(_directory);
        // The untrusted original filename is never part of a filesystem path.
        var name = Guid.NewGuid().ToString("N") + image.Extension;
        var path = Path.Combine(_directory, name);
        var created = false;
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await output.WriteAsync(image.Bytes, cancellationToken);
        }
        catch
        {
            if (created && File.Exists(path)) File.Delete(path);
            throw;
        }
        return "/images/products/" + name;
    }
}
