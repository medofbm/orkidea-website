using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models;

public static class OrderSources
{
    public const string Website = "Website", Facebook = "FacebookManual", Legacy = "LegacyUnknown";
    public static string Label(string? source) => source switch
    {
        Website => "الموقع",
        Facebook => "فيسبوك — إدخال يدوي",
        _ => "غير محدد — سجل سابق"
    };
}

public sealed class OrderCreation
{
    [Key] public int OrderId { get; set; }
    [MaxLength(32)] public required string Source { get; set; }
    [MaxLength(450)] public string? ActorUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
