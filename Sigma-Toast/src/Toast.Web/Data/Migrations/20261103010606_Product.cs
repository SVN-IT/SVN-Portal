using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Product : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Toast_Product",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PcsPerBox = table.Column<int>(type: "int", nullable: false),
                    OuterType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BoxPerOuter = table.Column<int>(type: "int", nullable: true),
                    ViindooQtyPerCode = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toast_Product", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Product_Barcode",
                schema: "dbo",
                table: "Toast_Product",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Product_PartNumber",
                schema: "dbo",
                table: "Toast_Product",
                column: "PartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Product_ProductId",
                schema: "dbo",
                table: "Toast_Product",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Toast_Product",
                schema: "dbo");
        }
    }
}
