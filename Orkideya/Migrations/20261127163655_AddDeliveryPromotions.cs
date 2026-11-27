using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OriginalShippingCost",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryPromotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MinSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPromotions", x => x.Id);
                    table.CheckConstraint("CK_DeliveryPromotion_Facts", "[Scope] IN ('Tripoli','AllLibya') AND [EndsAtUtc] > [StartsAtUtc] AND ([MinSubtotal] IS NULL OR [MinSubtotal] >= 0) AND LEN(LTRIM(RTRIM([Title]))) > 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_PromotionShipping",
                table: "Orders",
                sql: "[OriginalShippingCost] IS NULL OR ([OriginalShippingCost] > 0 AND [ShippingCost] = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryPromotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_PromotionShipping",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OriginalShippingCost",
                table: "Orders");
        }
    }
}
