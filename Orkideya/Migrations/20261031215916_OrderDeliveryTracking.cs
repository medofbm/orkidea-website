using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class OrderDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderDeliveries",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CarrierName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ShipmentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDeliveries", x => x.OrderId);
                    table.CheckConstraint("CK_OrderDelivery_State", "[Mode] IN ('Company','Self') AND (([Mode]='Company' AND [CarrierName] IS NOT NULL AND LEN(LTRIM(RTRIM([CarrierName])))>0) OR ([Mode]='Self' AND [CarrierName] IS NULL)) AND ([ShipmentReference] IS NULL OR LEN(LTRIM(RTRIM([ShipmentReference])))>0) AND [State] IN ('HandedOver','Delivered','Returning','Returned') AND [Revision]>0 AND [UpdatedAtUtc]>=[CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_OrderDeliveries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderDeliveryEvents",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PreviousState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NewState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CarrierName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ShipmentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LegacyPreparationConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ReturnReceiptConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    SamePackageReuseConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDeliveryEvents", x => x.RequestId);
                    table.CheckConstraint("CK_OrderDeliveryEvent_Data", "[Mode] IN ('Company','Self') AND (([Mode]='Company' AND [CarrierName] IS NOT NULL AND LEN(LTRIM(RTRIM([CarrierName])))>0) OR ([Mode]='Self' AND [CarrierName] IS NULL)) AND ([ShipmentReference] IS NULL OR LEN(LTRIM(RTRIM([ShipmentReference])))>0) AND [Revision]>0 AND LEN(LTRIM(RTRIM([ActorUserId])))>0 AND LEN([RequestHash])=64 AND [RequestId]<>'00000000-0000-0000-0000-000000000000' AND ([LegacyPreparationConfirmed]=0 OR [Action]='HandOver') AND [ReturnReceiptConfirmed]=CASE WHEN [Action]='ReceiveReturn' THEN 1 ELSE 0 END AND [SamePackageReuseConfirmed]=CASE WHEN [Action]='Redispatch' THEN 1 ELSE 0 END AND ([Action] NOT IN ('MarkReturning','Resume','Redispatch','CorrectDetails','CorrectState') OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason])))>0))");
                    table.CheckConstraint("CK_OrderDeliveryEvent_Transition", "([Action]='HandOver' AND [PreviousState] IS NULL AND [NewState]='HandedOver') OR ([PreviousState] IS NOT NULL AND (([Action]='Deliver' AND [PreviousState]='HandedOver' AND [NewState]='Delivered') OR ([Action]='MarkReturning' AND [PreviousState]='HandedOver' AND [NewState]='Returning') OR ([Action]='ReceiveReturn' AND [PreviousState]='Returning' AND [NewState]='Returned') OR ([Action]='Resume' AND [PreviousState]='Returning' AND [NewState]='HandedOver') OR ([Action]='Redispatch' AND [PreviousState]='Returned' AND [NewState]='HandedOver') OR ([Action]='CorrectDetails' AND [PreviousState]=[NewState] AND [NewState] IN ('HandedOver','Delivered','Returning','Returned')) OR ([Action]='CorrectState' AND (([PreviousState]='Delivered' AND [NewState]='HandedOver') OR ([PreviousState]='Returned' AND [NewState]='Returning')))))");
                    table.ForeignKey(
                        name: "FK_OrderDeliveryEvents_OrderDeliveries_OrderId",
                        column: x => x.OrderId,
                        principalTable: "OrderDeliveries",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDeliveries_State_UpdatedAtUtc",
                table: "OrderDeliveries",
                columns: new[] { "State", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDeliveryEvents_OrderId_Revision",
                table: "OrderDeliveryEvents",
                columns: new[] { "OrderId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OrderDeliveries]) OR EXISTS (SELECT 1 FROM [OrderDeliveryEvents]) THROW 51004, 'Cannot discard delivery state or audit history. Use a forward fix.', 1;");
            migrationBuilder.DropTable(
                name: "OrderDeliveryEvents");

            migrationBuilder.DropTable(
                name: "OrderDeliveries");
        }
    }
}
