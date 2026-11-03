using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class OrderShippingQuoteSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderShippingQuotes",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ServiceId = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    City = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginRevision = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HandoffMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ShippingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuotedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderShippingQuotes", x => x.OrderId);
                    table.CheckConstraint("CK_OrderShippingQuote_Facts", "[Provider]='Sabil' AND [Currency]='lyd' AND [ShippingFee]>=0 AND LEN([ServiceId])=24 AND [ServiceId] NOT LIKE '%[^0-9a-fA-F]%' AND LEN(LTRIM(RTRIM([BranchId])))>0 AND LEN(LTRIM(RTRIM([City])))>0 AND LEN(LTRIM(RTRIM([Area])))>0 AND LEN(LTRIM(RTRIM([OriginRevision])))>0 AND [HandoffMode] IN ('OfficeDropoff','CarrierCollection') AND [AcceptedAtUtc]>=[QuotedAtUtc] AND [AcceptedAtUtc]<[ExpiresAtUtc] AND DATEDIFF_BIG(millisecond,[QuotedAtUtc],[ExpiresAtUtc]) BETWEEN 1 AND 300000 AND DATEPART(TZOFFSET,[QuotedAtUtc])=0 AND DATEPART(TZOFFSET,[ExpiresAtUtc])=0 AND DATEPART(TZOFFSET,[AcceptedAtUtc])=0");
                    table.ForeignKey(
                        name: "FK_OrderShippingQuotes_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OrderShippingQuotes]) THROW 51006, 'Recorded shipping quote snapshots must not be erased. Apply a forward correction.', 1;");
            migrationBuilder.DropTable(
                name: "OrderShippingQuotes");
        }
    }
}
