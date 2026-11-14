using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameDefaultCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PartNumber",
                schema: "dbo",
                table: "Toast_Product",
                newName: "DefaultCode");

            migrationBuilder.RenameIndex(
                name: "IX_Toast_Product_PartNumber",
                schema: "dbo",
                table: "Toast_Product",
                newName: "IX_Toast_Product_DefaultCode");

            migrationBuilder.RenameColumn(
                name: "PartNumber",
                schema: "dbo",
                table: "Toast_Package",
                newName: "DefaultCode");

            migrationBuilder.RenameColumn(
                name: "PartNumber",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "DefaultCode");

            migrationBuilder.RenameIndex(
                name: "IX_Toast_LabelTemplate_PartNumber_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "IX_Toast_LabelTemplate_DefaultCode_Type_Operation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DefaultCode",
                schema: "dbo",
                table: "Toast_Product",
                newName: "PartNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Toast_Product_DefaultCode",
                schema: "dbo",
                table: "Toast_Product",
                newName: "IX_Toast_Product_PartNumber");

            migrationBuilder.RenameColumn(
                name: "DefaultCode",
                schema: "dbo",
                table: "Toast_Package",
                newName: "PartNumber");

            migrationBuilder.RenameColumn(
                name: "DefaultCode",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "PartNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Toast_LabelTemplate_DefaultCode_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "IX_Toast_LabelTemplate_PartNumber_Type_Operation");
        }
    }
}
