using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orkideya.Data;

namespace Orkideya.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261113120000_DeliveryHandoffMode")]
public partial class DeliveryHandoffMode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "OrderDeliveries", "OrderDeliveryEvents" })
        {
            migrationBuilder.AddColumn<string>(name: "HandoffMode", table: table, type: "nvarchar(32)", maxLength: 32, nullable: true);
            migrationBuilder.AddCheckConstraint(name: table == "OrderDeliveries" ? "CK_OrderDelivery_Handoff" : "CK_OrderDeliveryEvent_Handoff", table: table,
                sql: "[HandoffMode] IS NULL OR ([Mode]='Company' AND [HandoffMode] IN ('OfficeDropoff','CarrierCollection'))");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Refuse silent loss of recorded operational evidence.
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OrderDeliveries] WHERE [HandoffMode] IS NOT NULL) OR EXISTS (SELECT 1 FROM [OrderDeliveryEvents] WHERE [HandoffMode] IS NOT NULL) THROW 51007, 'Cannot discard recorded delivery handoff evidence.', 1;");
        foreach (var table in new[] { "OrderDeliveries", "OrderDeliveryEvents" })
        {
            migrationBuilder.DropCheckConstraint(name: table == "OrderDeliveries" ? "CK_OrderDelivery_Handoff" : "CK_OrderDeliveryEvent_Handoff", table: table);
            migrationBuilder.DropColumn(name: "HandoffMode", table: table);
        }
    }
}
