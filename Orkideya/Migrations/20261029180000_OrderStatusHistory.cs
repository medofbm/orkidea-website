using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Orkideya.Migrations;

public partial class OrderStatusHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "StatusRevision", table: "Orders", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.CreateTable(name: "OrderStatusChanges", columns: table => new
        {
            OrderStatusChangeId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            OrderId = table.Column<int>(type: "int", nullable: false),
            Revision = table.Column<int>(type: "int", nullable: false),
            PreviousStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
            NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
            ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
            ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_OrderStatusChanges", x => x.OrderStatusChangeId);
            table.ForeignKey("FK_OrderStatusChanges_Orders_OrderId", x => x.OrderId, "Orders", "OrderId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_OrderStatusChanges_OrderId_Revision", table: "OrderStatusChanges", columns: new[] { "OrderId", "Revision" }, unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never silently discard operational history during rollback.
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OrderStatusChanges]) THROW 51002, 'Order status history must be preserved before downgrade.', 1;");
        migrationBuilder.DropTable("OrderStatusChanges");
        migrationBuilder.DropColumn("StatusRevision", "Orders");
    }
}
