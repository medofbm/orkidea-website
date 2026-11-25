namespace Orkideya.Services;

/// <summary>
/// Runs once in the background after startup and creates missing WebP variants for existing product images.
/// Best effort: sequential, skips files over 10 MB, does nothing if the folder is missing or the feature is off
/// (config Images:BackfillVariants, default true). Does not delay readiness.
/// </summary>
public sealed class ProductImageBackfillService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ProductImageResizer resizer,
    ILogger<ProductImageBackfillService> logger) : BackgroundService
{
    private const long MaxFileBytes = 10 * 1024 * 1024;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => Run(stoppingToken), stoppingToken);

    private void Run(CancellationToken token)
    {
        try
        {
            if (!configuration.GetValue("Images:BackfillVariants", true)) return;
            if (string.IsNullOrEmpty(environment.WebRootPath)) return;
            var directory = Path.Combine(environment.WebRootPath, "images", "products");
            if (!Directory.Exists(directory)) return;
            int created = 0, failed = 0, skipped = 0;
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (token.IsCancellationRequested) break;
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg")) continue;
                if (ProductImageResizer.HasAllVariants(file)) continue;
                if (new FileInfo(file).Length > MaxFileBytes) { skipped++; continue; }
                if (resizer.TryCreateVariants(file)) created++; else failed++;
            }
            logger.LogInformation("Image variant backfill finished: {Created} created, {Failed} failed, {Skipped} skipped", created, failed, skipped);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image variant backfill stopped early");
        }
    }
}
