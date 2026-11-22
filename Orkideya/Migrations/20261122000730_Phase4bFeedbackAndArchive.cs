using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class Phase4bFeedbackAndArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowName",
                table: "Reviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Reviews",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Reviews",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "site");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ProductVariants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "ContactMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderId",
                table: "Reviews",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Orders_OrderId",
                table: "Reviews",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.SetNull);

            // The five real Facebook comments that used to live in frontend/data/testimonials.ts.
            // Fixed literals only, so the migration is repeatable and has no time or environment dependence.
            migrationBuilder.InsertData(
                table: "Reviews",
                columns: new[] { "CustomerName", "Message", "Rating", "IsVisible", "CreatedAtUtc", "Source", "AllowName" },
                values: new object[,]
                {
                { "", "حاجة طبيعية واكويسة... بالتوفيق", 5, true, new DateTime(2026, 11, 21, 0, 0, 0, 0, DateTimeKind.Utc), "facebook", false },
                { "", "جربت جميع زيوت عندك ممتازة وروعة", 5, true, new DateTime(2026, 11, 21, 0, 0, 0, 0, DateTimeKind.Utc), "facebook", false },
                { "", "انى جربت زيت السمسم ونتيجته تبارك الرحمن روووعة 💕 وبالنسبة لزيت جوز الهند مانستغناش عليه نهااائى لأنه فوق الروعة لكل شئ 💕💕", 5, true, new DateTime(2026, 11, 21, 0, 0, 0, 0, DateTimeKind.Utc), "facebook", false },
                { "", "ربي يعاونك ويفتحها عليك ❤️", 5, true, new DateTime(2026, 11, 21, 0, 0, 0, 0, DateTimeKind.Utc), "facebook", false },
                { "", "انى جربته منكم بسم الله ماشاء الله فعلا مفعوله ممتاز للشعر 🌹", 5, true, new DateTime(2026, 11, 21, 0, 0, 0, 0, DateTimeKind.Utc), "facebook", false }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the imported Facebook reviews before the Source column is dropped.
            migrationBuilder.Sql("DELETE FROM [Reviews] WHERE [Source] = N'facebook'");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Orders_OrderId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_OrderId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "AllowName",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "ContactMessages");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Categories");
        }
    }
}
