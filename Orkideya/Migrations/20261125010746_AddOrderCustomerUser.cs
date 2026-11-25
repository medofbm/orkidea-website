using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCustomerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerUserId",
                table: "Orders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerUserId",
                table: "Orders",
                column: "CustomerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "Orders");
        }
    }
}
