using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingRegionAndDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryDuration",
                table: "ShippingRates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "ShippingRates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryDuration",
                table: "ShippingRates");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "ShippingRates");
        }
    }
}
