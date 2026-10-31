using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models;

public sealed class OrderStatusChange
{
    public int OrderStatusChangeId { get; set; }
    public int OrderId { get; set; }
    public int Revision { get; set; }
    [MaxLength(32)] public required string PreviousStatus { get; set; }
    [MaxLength(32)] public required string NewStatus { get; set; }
    [MaxLength(450)] public required string ActorUserId { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
