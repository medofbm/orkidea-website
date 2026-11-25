using System.IO.Compression;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Orkideya.Areas.Identity.Pages.Account;
using Orkideya.Controllers;
using Orkideya.Services;
using Orkideya.Services.Email;

/// <summary>Contact form validation, email link base URL, Identity page rate limits and image size caps. No database.</summary>
internal static class SecurityHardeningRegressionB
{
    public static async Task Run(Action<bool, string> check)
    {
        // M-2 / F-9: legacy contact form
        var send = typeof(HomeController).GetMethod("SendMessage")!;
        check(send.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName == "public-forms",
            "Legacy contact form uses the public-forms limiter");
        check(ContactApiController.Validate(new("Ali", "+218911234567", null, new string('x', 5000)),
            out _, out _, out _, out _) is not null, "Legacy contact form rejects an oversized message");
        check(ContactApiController.Validate(new("Ali", "abc", null, "رسالة طويلة بما يكفي"),
            out _, out _, out _, out _) is not null, "Legacy contact form rejects a bad phone number");
        check(ContactApiController.Validate(new("Ali", null, "not-an-email", "رسالة طويلة بما يكفي"),
            out _, out _, out _, out _) is not null, "Legacy contact form rejects a bad email");

        // F-5 / L-1: Identity pages
        foreach (var (type, policy) in new (Type, string)[]
        {
            (typeof(ForgotPasswordModel), "account-recovery"),
            (typeof(ResendEmailConfirmationModel), "account-recovery"),
            (typeof(ResetPasswordModel), "account-recovery"),
            (typeof(RegisterModel), "account-sign-up"),
            (typeof(LoginModel), "account-sign-in"),
            (typeof(LoginWith2faModel), "account-sign-in"),
        })
            check(type.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName == policy,
                $"{type.Name} uses the {policy} limiter");

        check(PublicLinks.Combine("https://shop.example.test/", "/Identity/Account/ResetPassword?code=abc", false, "http", "evil.example")
            == "https://shop.example.test/Identity/Account/ResetPassword?code=abc", "Email link uses the configured base URL, not the request host");
        check(PublicLinks.Combine("", "/x", false, "https", "evil.example") is null, "No base URL in production: refuse to send");
        check(PublicLinks.Combine("  ", "/x", false, "https", "evil.example") is null, "Blank base URL in production: refuse to send");
        check(PublicLinks.Combine("", "/x", true, "http", "localhost:5000") == "http://localhost:5000/x", "No base URL in Development: request origin fallback");
        check(PublicLinks.Combine("ftp://shop.example.test", "/x", true, "http", "localhost") is null, "Non-http base URL is refused");
        check(PublicLinks.Combine("https://shop.example.test", "//evil.example/x", false, "https", "h") is null, "Protocol-relative path is refused");
        check(PublicLinks.Combine("https://shop.example.test", "https://evil.example/x", false, "https", "h") is null, "Absolute path is refused");

        // M-1: decompression bomb
        var bomb = MakePngHeader(20000, 20000);
        check(!ProductImageResizer.IsWithinLimits(20000, 20000), "20000x20000 is over the limit");
        check(!ProductImageResizer.IsWithinLimits(8001, 100), "Width over 8000 is rejected");
        check(!ProductImageResizer.IsWithinLimits(100, 8001), "Height over 8000 is rejected");
        check(!ProductImageResizer.IsWithinLimits(7000, 6000), "Over 40 megapixels is rejected");
        check(ProductImageResizer.IsWithinLimits(8000, 5000), "40 megapixels exactly is accepted");
        check(!ProductImageResizer.HeaderWithinLimits(bomb), "Crafted 20000x20000 PNG header is rejected from the header alone");
        check(ProductImageResizer.HeaderWithinLimits(MakePngHeader(100, 80)), "Small PNG header is accepted");

        var dir = Path.Combine(Path.GetTempPath(), "orkidea-bomb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var resizer = new ProductImageResizer(NullLogger<ProductImageResizer>.Instance);
            var path = Path.Combine(dir, "bomb.png");
            File.WriteAllBytes(path, bomb);
            check(!resizer.TryCreateVariants(path), "Resizer skips the oversized file without crashing");
            check(!ProductImageResizer.Widths.Any(w => File.Exists(ProductImageResizer.VariantPath(path, w))), "No variants written for the oversized file");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        using var bombStream = new MemoryStream(bomb);
        var bombFile = new FormFile(bombStream, 0, bomb.Length, "file", "bomb.png");
        string? message = null;
        try { await ProductImageStorage.ReadValidatedAsync(bombFile); }
        catch (InvalidDataException ex) { message = ex.Message; }
        check(message is not null && message.Contains("8000 بكسل"), "Upload of the oversized PNG gets the Arabic dimension error");

        var ok = MakePngHeader(100, 80);
        using var okStream = new MemoryStream(ok);
        var okFile = new FormFile(okStream, 0, ok.Length, "file", "ok.png");
        var (bytes, ext) = await ProductImageStorage.ReadValidatedAsync(okFile);
        check(bytes.Length == ok.Length && ext == ".png", "Upload of a normal-sized PNG header still passes validation");
    }

    /// <summary>A structurally valid PNG whose IHDR declares the given size and whose IDAT is tiny (so it is never a real image).</summary>
    private static byte[] MakePngHeader(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        WriteInt(ihdr, 0, width);
        WriteInt(ihdr, 4, height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // RGB
        WriteChunk(output, "IHDR", ihdr);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(new byte[] { 0, 0, 0, 0 });
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void WriteInt(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24); target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8); target[offset + 3] = (byte)value;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);
        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(body, 0);
        data.CopyTo(body, 4);
        output.Write(body);
        var crc = new byte[4];
        WriteInt(crc, 0, unchecked((int)Crc32(body)));
        output.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
