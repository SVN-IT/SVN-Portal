using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrintTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrintTarget",
                schema: "dbo",
                table: "Toast_SessionConfirm",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintTarget",
                schema: "dbo",
                table: "Toast_Package",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrintTarget",
                schema: "dbo",
                table: "Toast_SessionConfirm");

            migrationBuilder.DropColumn(
                name: "PrintTarget",
                schema: "dbo",
                table: "Toast_Package");
        }
    }
}
