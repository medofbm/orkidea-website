using Microsoft.EntityFrameworkCore.Migrations;

namespace Orkideya.Migrations;

public partial class CarrierDeliveryChoice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPickup", table: "OrderShippingQuotes", type: "bit",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(
            name: "ServiceKind", table: "OrderShippingQuotes", type: "nvarchar(16)",
            maxLength: 16, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsPickup", table: "OrderShippingQuotes");
        migrationBuilder.DropColumn(name: "ServiceKind", table: "OrderShippingQuotes");
    }
}
