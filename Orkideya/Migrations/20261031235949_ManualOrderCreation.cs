using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class ManualOrderCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderCreations",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCreations", x => x.OrderId);
                    table.CheckConstraint("CK_OrderCreation_Source", "([Source]='Website' AND [ActorUserId] IS NULL) OR ([Source]='FacebookManual' AND [ActorUserId] IS NOT NULL AND LEN(LTRIM(RTRIM([ActorUserId])))>0)");
                    table.ForeignKey(
                        name: "FK_OrderCreations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderCreations_Source_RecordedAtUtc",
                table: "OrderCreations",
                columns: new[] { "Source", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OrderCreations]) THROW 51005, 'Recorded creation provenance must not be erased. Apply a forward correction.', 1;");
            migrationBuilder.DropTable(
                name: "OrderCreations");
        }
    }
}
