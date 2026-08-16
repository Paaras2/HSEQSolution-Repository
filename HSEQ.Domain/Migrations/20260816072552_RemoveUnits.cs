using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSEQ.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Units_UnitId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UnitId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "Documents");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Documents",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(18)",
                oldMaxLength: 18);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Documents",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UnitId",
                table: "Documents",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Units_UnitId",
                table: "Documents",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
