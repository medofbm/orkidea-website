using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkideya.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountActivities",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSignInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignInCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountActivities", x => x.UserId);
                    table.CheckConstraint("CK_AccountActivity_SignInCount", "[SignInCount] >= 0");
                    table.ForeignKey(
                        name: "FK_AccountActivities_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountActivities");
        }
    }
}
