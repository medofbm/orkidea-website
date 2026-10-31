using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class BulkStockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StockUnitsPerItem",
                table: "ProductVariants",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "OrderStatusChanges",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockState",
                table: "Orders",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Untracked");

            migrationBuilder.AddColumn<int>(
                name: "StockAccountId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockUnit",
                table: "OrderItems",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockUnitsPerItem",
                table: "OrderItems",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockAccounts",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    OnHand = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAccounts", x => x.ProductId);
                    table.CheckConstraint("CK_StockAccount_Balances", "[OnHand] >= 0 AND [Reserved] >= 0 AND [Reserved] <= [OnHand] AND [OnHand] <= 1000000000 AND [Unit] IN ('ml','piece') AND ([Unit] <> 'piece' OR ([OnHand] = FLOOR([OnHand]) AND [Reserved] = FLOOR([Reserved])))");
                    table.ForeignKey(
                        name: "FK_StockAccounts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockAccountId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OnHandDelta = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReservedDelta = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.StockMovementId);
                    table.ForeignKey(
                        name: "FK_StockMovements_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockAccounts_StockAccountId",
                        column: x => x.StockAccountId,
                        principalTable: "StockAccounts",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StockAccountId",
                table: "OrderItems",
                column: "StockAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItem_StockSnapshot",
                table: "OrderItems",
                sql: "([StockAccountId] IS NULL AND [StockUnitsPerItem] IS NULL AND [StockUnit] IS NULL) OR ([StockAccountId] IS NOT NULL AND [StockAccountId] = [ProductId] AND [StockUnitsPerItem] IS NOT NULL AND [StockUnitsPerItem] > 0 AND [StockUnitsPerItem] <= 1000000 AND [StockUnit] IS NOT NULL AND [StockUnit] IN ('ml','piece') AND ([StockUnit] <> 'piece' OR [StockUnitsPerItem] = FLOOR([StockUnitsPerItem])))");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrderId_StockAccountId_Kind",
                table: "StockMovements",
                columns: new[] { "OrderId", "StockAccountId", "Kind" },
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StockAccountId",
                table: "StockMovements",
                column: "StockAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_StockAccounts_StockAccountId",
                table: "OrderItems",
                column: "StockAccountId",
                principalTable: "StockAccounts",
                principalColumn: "ProductId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM StockAccounts) OR EXISTS (SELECT 1 FROM StockMovements) OR EXISTS (SELECT 1 FROM Orders WHERE StockState <> N'Untracked') OR EXISTS (SELECT 1 FROM OrderStatusChanges WHERE Reason IS NOT NULL) THROW 51003, 'Stock configuration, movements and cancellation reasons must be preserved before downgrade.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_StockAccounts_StockAccountId",
                table: "OrderItems");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockAccounts");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_StockAccountId",
                table: "OrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItem_StockSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StockUnitsPerItem",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "OrderStatusChanges");

            migrationBuilder.DropColumn(
                name: "StockState",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StockAccountId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StockUnit",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StockUnitsPerItem",
                table: "OrderItems");
        }
    }
}
