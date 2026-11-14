using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RevertTemplateLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            // Đổi tên cột (không xoá / thêm) để giữ mã của các mẫu tem đang có. Tên dài hơn 50 ký tự thì lệnh đổi độ dài báo lỗi.
            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "PartNumber");

            migrationBuilder.AlterColumn<string>(
                name: "PartNumber",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Toast_LabelTemplate_PartNumber_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                columns: new[] { "PartNumber", "Type", "Operation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Toast_LabelTemplate_PartNumber_Type_Operation",
                schema: "dbo",
                table: "Toast_LabelTemplate");

            migrationBuilder.AlterColumn<string>(
                name: "PartNumber",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.RenameColumn(
                name: "PartNumber",
                schema: "dbo",
                table: "Toast_LabelTemplate",
                newName: "Name");

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
        }
    }
}
