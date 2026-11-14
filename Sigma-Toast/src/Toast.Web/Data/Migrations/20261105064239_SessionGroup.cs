using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ViindooMoId",
                schema: "dbo",
                table: "Toast_Session",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WoGroup",
                schema: "dbo",
                table: "Toast_Session",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WoGroup",
                schema: "dbo",
                table: "Toast_Package",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Dữ liệu đã có: nhóm = tên lệnh bỏ đuôi backorder (NM/MO/06230-002 -> NM/MO/06230)
            foreach (string table in new[] { "Toast_Session", "Toast_Package" })
                migrationBuilder.Sql(
                    $"UPDATE [dbo].[{table}] SET [WoGroup] = CASE WHEN CHARINDEX('-', [WoName]) > 0 " +
                    "THEN LEFT([WoName], CHARINDEX('-', [WoName]) - 1) ELSE [WoName] END WHERE [WoGroup] = N''");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Session_WoGroup_Status",
                schema: "dbo",
                table: "Toast_Session",
                columns: new[] { "WoGroup", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Package_WoGroup_Status",
                schema: "dbo",
                table: "Toast_Package",
                columns: new[] { "WoGroup", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Toast_Session_WoGroup_Status",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropIndex(
                name: "IX_Toast_Package_WoGroup_Status",
                schema: "dbo",
                table: "Toast_Package");

            migrationBuilder.DropColumn(
                name: "ViindooMoId",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropColumn(
                name: "WoGroup",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropColumn(
                name: "WoGroup",
                schema: "dbo",
                table: "Toast_Package");
        }
    }
}
