using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class TemplateLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Toast_LabelTemplate_PartNumber_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate");

            // Đổi tên cột (giữ dữ liệu): mẫu cũ mang tên = part number
            migrationBuilder.RenameColumn(
                name: "PartNumber",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "Name");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "BoxTemplateId",
                schema: "dbo",
                table: "Toast_Product",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OuterTemplateId",
                schema: "dbo",
                table: "Toast_Product",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Variables",
                schema: "dbo",
                table: "Toast_Product",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Product_BoxTemplateId",
                schema: "dbo",
                table: "Toast_Product",
                column: "BoxTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_Product_OuterTemplateId",
                schema: "dbo",
                table: "Toast_Product",
                column: "OuterTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_LabelTemplate_Name_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                columns: new[] { "Name", "Type", "Operation" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Toast_Product_Toast_LabelTemplate_BoxTemplateId",
                schema: "dbo",
                table: "Toast_Product",
                column: "BoxTemplateId",
                principalSchema: "dbo",
                principalTable: "Toast_LabelTemplate",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Toast_Product_Toast_LabelTemplate_OuterTemplateId",
                schema: "dbo",
                table: "Toast_Product",
                column: "OuterTemplateId",
                principalSchema: "dbo",
                principalTable: "Toast_LabelTemplate",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Giữ hành vi cũ: sản phẩm dùng mẫu Box có tên = part number (mẫu cũ tra theo part number)
            migrationBuilder.Sql(@"UPDATE p SET p.BoxTemplateId = (
    SELECT MIN(t.Id) FROM [dbo].[Toast_LabelTemplate] t WHERE t.Name = p.PartNumber AND t.Type = N'Box')
FROM [dbo].[Toast_Product] p
WHERE p.BoxTemplateId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Toast_Product_Toast_LabelTemplate_BoxTemplateId",
                schema: "dbo",
                table: "Toast_Product");

            migrationBuilder.DropForeignKey(
                name: "FK_Toast_Product_Toast_LabelTemplate_OuterTemplateId",
                schema: "dbo",
                table: "Toast_Product");

            migrationBuilder.DropIndex(
                name: "IX_Toast_Product_BoxTemplateId",
                schema: "dbo",
                table: "Toast_Product");

            migrationBuilder.DropIndex(
                name: "IX_Toast_Product_OuterTemplateId",
                schema: "dbo",
                table: "Toast_Product");

            migrationBuilder.DropIndex(
                name: "IX_Toast_LabelTemplate_Name_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate");

            migrationBuilder.DropColumn(
                name: "BoxTemplateId",
                schema: "dbo",
                table: "Toast_Product");

            migrationBuilder.DropColumn(
                name: "OuterTemplateId",
                schema: "dbo",
                table: "Toast_Product");

            migrationBuilder.DropColumn(
                name: "Variables",
                schema: "dbo",
                table: "Toast_Product");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "PartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Toast_LabelTemplate_PartNumber_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                columns: new[] { "PartNumber", "Type", "Operation" },
                unique: true);
        }
    }
}
