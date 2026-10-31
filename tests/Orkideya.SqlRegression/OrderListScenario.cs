using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orkideya.Application.Orders;
using Orkideya.Areas.Admin.Controllers;
using Orkideya.Data;
using Orkideya.Models;
using Orkideya.Services;

internal static class OrderListScenario
{
    public static async Task Run(Func<ApplicationDbContext> open, Action<bool, string> check)
    {
        await using var db = open();
        var marker = "ListQA_" + Guid.NewGuid().ToString("N");
        var rows = Enumerable.Range(0, 53).Select(i => new Order
        {
            CustomerName = marker, WhatsAppNumber = "0000000000", ShippingAddress = "Synthetic",
            City = "Synthetic", OrderDate = new DateTime(2043, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = i < 30 ? "Pending" : "Cancelled", TotalAmount = 3, ShippingCost = 1
        }).ToArray();
        db.Orders.AddRange(rows);
        await db.SaveChangesAsync();
        check(OrderListFilter.TryCreate(" ALL ", " " + marker + " ", out var filter), "Order-list search and status normalize surrounding whitespace");
        var queries = new OrderListQuery(db);
        var first = await queries.PageAsync(filter, 1);
        var second = await queries.PageAsync(filter, 2);
        var last = await queries.PageAsync(filter, int.MaxValue);
        check(first.Total == 53 && first.Pages == 3 && first.Orders.Count == 25 && second.Orders.Count == 25
            && last.Page == 3 && last.Orders.Count == 3, "SQL paging bounds loads to 25 and clamps huge pages without integer overflow");
        var ids = first.Orders.Concat(second.Orders).Concat(last.Orders).Select(o => o.OrderId).ToArray();
        check(ids.SequenceEqual(rows.Select(o => o.OrderId).OrderDescending()) && ids.Distinct().Count() == 53,
            "Equal timestamps use order identity as stable tie-breaker: no missing or repeated rows across pages");
        OrderListFilter.TryCreate("PENDING", marker, out var pending);
        var pendingPage = await queries.PageAsync(pending, 2);
        check(pendingPage.Total == 30 && pendingPage.Orders.Count == 5 && pendingPage.Orders.All(o => o.Status == "Pending"),
            "Search and status apply before count and pagination");
        OrderListFilter.TryCreate("all", marker + "_absent", out var empty);
        var noRows = await queries.PageAsync(empty, 100);
        check(noRows.Total == 0 && noRows.Page == 1 && noRows.Pages == 1 && noRows.Orders.Count == 0,
            "Empty filtered page has a coherent zero count and no phantom page");
        OrderListFilter.TryCreate("all", "#" + rows[0].OrderId, out var byId);
        check((await queries.PageAsync(byId, 1)).Orders.Single().OrderId == rows[0].OrderId, "Order number search accepts hash-prefixed exact identity");
        check(!OrderListFilter.TryCreate("not-a-status", "", out _) && !OrderListFilter.TryCreate("all", new string('x', 101), out _),
            "Unsupported status and oversized searches rejected");
        var invalidPage = false;
        try { await queries.PageAsync(filter, 0); }
        catch (ArgumentOutOfRangeException) { invalidPage = true; }
        check(invalidPage, "Nonpositive pages rejected before querying SQL");

        var controller = new OrdersController(db, new ExcelExportService(), new OrderStatusService(db, TimeProvider.System),
            NullLogger<OrdersController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var view = (ViewResult)await controller.Index("pending", marker, 2);
        check(((IEnumerable<Order>)view.Model!).Count() == 5 && (int)controller.ViewBag.TotalOrders == 30
            && (int)controller.ViewBag.CurrentPage == 2, "Admin action returns page rows with the filtered total, not page size as the total");
        var file = (FileContentResult)await controller.ExportToExcel("pending", marker);
        using var stream = new MemoryStream(file.FileContents);
        using var workbook = new XLWorkbook(stream);
        var exported = workbook.Worksheet(1).RowsUsed().Skip(1).Select(r => r.Cell(1).GetValue<int>()).ToArray();
        check(exported.SequenceEqual(rows.Where(o => o.Status == "Pending").Select(o => o.OrderId).OrderDescending()),
            "XLSX exports every filtered order in the same stable order, including rows beyond the displayed page");
        check(await controller.Index(page: -1) is BadRequestObjectResult
            && await controller.ExportToExcel(status: "unknown") is BadRequestObjectResult,
            "Admin list and export enforce the same bounded filter contract");
    }
}
