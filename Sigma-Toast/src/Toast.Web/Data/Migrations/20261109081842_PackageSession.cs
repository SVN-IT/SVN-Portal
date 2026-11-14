using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PackageSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Toast_Package_Toast_SessionConfirm_SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package");

            migrationBuilder.RenameColumn(
                name: "SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package",
                newName: "SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_Toast_Package_SessionConfirmId",
                schema: "dbo",
                table: "Toast_Package",
                newName: "IX_Toast_Package_SessionId");

            // Cột vừa đổi tên đang chứa id lần xác nhận (Toast_SessionConfirm.Id): đổi thành id phiên của lần xác nhận đó
            migrationBuilder.Sql(@"UPDATE p SET p.[SessionId] = c.[SessionId]
FROM [dbo].[Toast_Package] p
JOIN [dbo].[Toast_SessionConfirm] c ON c.[Id] = p.[SessionId];");

            migrationBuilder.AddForeignKey(
                name: "FK_Toast_Package_Toast_Session_SessionId",
                schema: "dbo",
                table: "Toast_Package",
                column: "SessionId",
                principalSchema: "dbo",
                principalTable: "Toast_Session",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Toast_Package_Toast_Session_SessionId",
                schema: "dbo",
                table: "Toast_Package");

            migrationBuilder.RenameColumn(
                name: "SessionId",
                schema: "dbo",
                table: "Toast_Package",
                newName: "SessionConfirmId");

            migrationBuilder.RenameIndex(
                name: "IX_Toast_Package_SessionId",
                schema: "dbo",
                table: "Toast_Package",
                newName: "IX_Toast_Package_SessionConfirmId");

            // Quay lại: id phiên -> lần xác nhận mới nhất của phiên đó
            migrationBuilder.Sql(@"UPDATE p SET p.[SessionConfirmId] =
    (SELECT MAX(c.[Id]) FROM [dbo].[Toast_SessionConfirm] c WHERE c.[SessionId] = p.[SessionConfirmId])
FROM [dbo].[Toast_Package] p
WHERE p.[SessionConfirmId] IS NOT NULL;");

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
    }
}
