using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Package : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Toast_Package",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WoName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PackageNo = table.Column<int>(type: "int", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrinterId = table.Column<int>(type: "int", nullable: true),
                    PrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrintError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InputLogId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toast_Package", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Toast_Package_Toast_Printer_PrinterId",
                        column: x => x.PrinterId,
                        principalSchema: "dbo",
                        principalTable: "Toast_Printer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Package_PrinterId",
                schema: "dbo",
                table: "Toast_Package",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Package_WoName_PackageNo",
                schema: "dbo",
                table: "Toast_Package",
                columns: new[] { "WoName", "PackageNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Package_WoName_Status",
                schema: "dbo",
                table: "Toast_Package",
                columns: new[] { "WoName", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Toast_Package",
                schema: "dbo");
        }
    }
}
