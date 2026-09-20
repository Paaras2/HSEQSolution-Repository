using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSEQ.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByPCode",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPCode",
                table: "OrganizationalManagements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "OrganizationalManagements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPCode",
                table: "OrganizationalActivities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "OrganizationalActivities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPCode",
                table: "LegacyDocumentNumbers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "LegacyDocumentNumbers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPCode",
                table: "HeadquartersCodeCounters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "HeadquartersCodeCounters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPCode",
                table: "DocumentTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "DocumentTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByPCode",
                table: "Documents",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByPCode",
                table: "DocumentRelations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "DocumentRelations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPCode",
                table: "Admins",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByPCode",
                table: "Admins",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByPCode",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreatedByPCode",
                table: "OrganizationalManagements");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "OrganizationalManagements");

            migrationBuilder.DropColumn(
                name: "CreatedByPCode",
                table: "OrganizationalActivities");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "OrganizationalActivities");

            migrationBuilder.DropColumn(
                name: "CreatedByPCode",
                table: "LegacyDocumentNumbers");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "LegacyDocumentNumbers");

            migrationBuilder.DropColumn(
                name: "CreatedByPCode",
                table: "HeadquartersCodeCounters");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "HeadquartersCodeCounters");

            migrationBuilder.DropColumn(
                name: "CreatedByPCode",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "DocumentRelations");

            migrationBuilder.DropColumn(
                name: "CreatedByPCode",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "ModifiedByPCode",
                table: "Admins");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByPCode",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByPCode",
                table: "DocumentRelations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
