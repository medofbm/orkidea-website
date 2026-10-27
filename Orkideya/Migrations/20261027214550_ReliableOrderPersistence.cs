using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class ReliableOrderPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse to hide legacy orphan rows. Reconcile them before a guarded upgrade.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM OrderItems i LEFT JOIN Orders o ON i.OrderId = o.OrderId WHERE o.OrderId IS NULL) THROW 51001, 'Order item orphans must be reconciled before this migration.', 1;");
            migrationBuilder.AddColumn<string>(
                name: "CheckoutRequestHash",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutToken",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CheckoutToken",
                table: "Orders",
                column: "CheckoutToken",
                unique: true,
                filter: "[CheckoutToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Orders_OrderId",
                table: "OrderItems",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Orders_OrderId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CheckoutToken",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "CheckoutRequestHash",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CheckoutToken",
                table: "Orders");
        }
    }
}
