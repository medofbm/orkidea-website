using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models;

public static class NotificationState
{
    public const string Pending = "Pending", Processing = "Processing", Accepted = "Accepted",
        Failed = "Failed", NeedsReview = "NeedsReview";
}

public sealed class OrderNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    [MaxLength(16)] public string State { get; set; } = NotificationState.Pending;
    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    [MaxLength(64)] public string? ProviderMessageId { get; set; }
    [MaxLength(64)] public string? LastFailureCode { get; set; }
}
