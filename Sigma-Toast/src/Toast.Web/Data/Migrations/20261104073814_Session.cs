using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Session : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Toast_Session",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WoName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    DefaultCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toast_Session", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Toast_SessionConfirm",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    EmployeeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrinterId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Values = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Warnings = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toast_SessionConfirm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Toast_SessionConfirm_Toast_Printer_PrinterId",
                        column: x => x.PrinterId,
                        principalSchema: "dbo",
                        principalTable: "Toast_Printer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Toast_SessionConfirm_Toast_Session_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "dbo",
                        principalTable: "Toast_Session",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Package_SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package",
                column: "SessionConfirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Session_WoName_Status",
                schema: "dbo",
                table: "Toast_Session",
                columns: new[] { "WoName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Toast_SessionConfirm_EmployeeCode",
                schema: "dbo",
                table: "Toast_SessionConfirm",
                column: "EmployeeCode");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_SessionConfirm_PrinterId",
                schema: "dbo",
                table: "Toast_SessionConfirm",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_SessionConfirm_SessionId",
                schema: "dbo",
                table: "Toast_SessionConfirm",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Toast_Package_Toast_SessionConfirm_SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package",
                column: "SessionConfirmId",
                principalSchema: "dbo",
                principalTable: "Toast_SessionConfirm",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Toast_Package_Toast_SessionConfirm_SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package");

            migrationBuilder.DropTable(
                name: "Toast_SessionConfirm",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Toast_Session",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Toast_Package_SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package");

            migrationBuilder.DropColumn(
                name: "SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package");
        }
    }
}
