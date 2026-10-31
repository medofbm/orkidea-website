namespace Orkideya.Areas.Admin.Models;

public sealed class DeliveryIndexViewModel
{
    public required List<DeliveryListRow> Rows { get; init; }
    public required string State { get; init; }
    public required string Search { get; init; }
    public int Page { get; init; }
    public int Pages { get; init; }
    public int Total { get; init; }
}

public sealed class DeliveryListRow
{
    public int OrderId { get; init; }
    public required string CustomerName { get; init; }
    public required string City { get; init; }
    public required string Mode { get; init; }
    public string? CarrierName { get; init; }
    public string? ShipmentReference { get; init; }
    public required string State { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
