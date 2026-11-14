using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                schema: "dbo",
                table: "Toast_Session",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastScanSeq",
                schema: "dbo",
                table: "Toast_Session",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceId",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropColumn(
                name: "LastScanSeq",
                schema: "dbo",
                table: "Toast_Session");
        }
    }
}
