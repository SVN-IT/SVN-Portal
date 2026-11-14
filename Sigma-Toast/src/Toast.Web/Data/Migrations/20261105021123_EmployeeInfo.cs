using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeName",
                schema: "dbo",
                table: "Toast_SessionConfirm",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeePhone",
                schema: "dbo",
                table: "Toast_SessionConfirm",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeName",
                schema: "dbo",
                table: "Toast_SessionConfirm");

            migrationBuilder.DropColumn(
                name: "EmployeePhone",
                schema: "dbo",
                table: "Toast_SessionConfirm");
        }
    }
}
