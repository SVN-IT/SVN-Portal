using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScanSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScanCode",
                schema: "dbo",
                table: "Toast_SessionConfirm",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanSource",
                schema: "dbo",
                table: "Toast_Product",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Barcode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScanCode",
                schema: "dbo",
                table: "Toast_SessionConfirm");

            migrationBuilder.DropColumn(
                name: "ScanSource",
                schema: "dbo",
                table: "Toast_Product");
        }
    }
}
