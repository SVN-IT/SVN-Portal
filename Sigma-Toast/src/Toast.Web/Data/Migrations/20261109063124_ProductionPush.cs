using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toast.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PushDetail",
                schema: "dbo",
                table: "Toast_Session",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushMessage",
                schema: "dbo",
                table: "Toast_Session",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PushQty",
                schema: "dbo",
                table: "Toast_Session",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushStatus",
                schema: "dbo",
                table: "Toast_Session",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PushedAt",
                schema: "dbo",
                table: "Toast_Session",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PushDetail",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropColumn(
                name: "PushMessage",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropColumn(
                name: "PushQty",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropColumn(
                name: "PushStatus",
                schema: "dbo",
                table: "Toast_Session");

            migrationBuilder.DropColumn(
                name: "PushedAt",
                schema: "dbo",
                table: "Toast_Session");
        }
    }
}
