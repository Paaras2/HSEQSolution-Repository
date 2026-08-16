using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSEQ.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentNumberingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            // --- Pre-existing placeholder/test data cleanup -----------------------------
            // Confirmed across three independent prior audits (backend audit, Phase 0,
            // Phase 0.5) that every row in Documents at this point is Swagger-default
            // placeholder data, not real business data: Number = 'string' (17/17 rows,
            // identical), FileName = 'string.jpg' (17/17, identical), RelatedDocumentId =
            // the exact Swagger example GUID 3fa85f64-5717-4562-b3fc-2c963f66afa6 (17/17,
            // all orphaned - no such document ever existed), all 17 attached to a single
            // test Unit, CreatedTime at the CLR default (0001-01-01) for every row.
            //
            // These rows cannot be assigned real values for the new required Project /
            // OrganizationalManagement / OrganizationalActivity / DocumentType foreign
            // keys (no real classification exists for them - fabricating one would create
            // new fake data rather than clean up old fake data), and the unique index on
            // Number cannot be created while all 17 share the same value. They are removed
            // here, narrowly, by the exact confirmed placeholder value only - this
            // statement will not match any row that is not that literal placeholder.
            migrationBuilder.Sql("DELETE FROM [Documents] WHERE [Number] = N'string';");
            migrationBuilder.CreateSequence<int>(
                name: "DocumentSerialSequence",
                schema: "dbo",
                minValue: 1L,
                maxValue: 999L);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Units",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Documents",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "ContentRevision",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentTypeId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationalActivityId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationalManagementId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "SerialNumber",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationalManagements",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationalManagements", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsProjectRelated = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationalActivities",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrganizationalManagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationalActivities", x => x.Key);
                    table.ForeignKey(
                        name: "FK_OrganizationalActivities_OrganizationalManagements_OrganizationalManagementId",
                        column: x => x.OrganizationalManagementId,
                        principalTable: "OrganizationalManagements",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Number",
                table: "Documents",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OrganizationalActivityId",
                table: "Documents",
                column: "OrganizationalActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OrganizationalManagementId",
                table: "Documents",
                column: "OrganizationalManagementId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalActivities_OrganizationalManagementId_Code",
                table: "OrganizationalActivities",
                columns: new[] { "OrganizationalManagementId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalManagements_Code",
                table: "OrganizationalManagements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                table: "Projects",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentTypes_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_OrganizationalActivities_OrganizationalActivityId",
                table: "Documents",
                column: "OrganizationalActivityId",
                principalTable: "OrganizationalActivities",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_OrganizationalManagements_OrganizationalManagementId",
                table: "Documents",
                column: "OrganizationalManagementId",
                principalTable: "OrganizationalManagements",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Projects_ProjectId",
                table: "Documents",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DocumentTypes_DocumentTypeId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_OrganizationalActivities_OrganizationalActivityId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_OrganizationalManagements_OrganizationalManagementId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Projects_ProjectId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "OrganizationalActivities");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "OrganizationalManagements");

            migrationBuilder.DropIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_Number",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_OrganizationalActivityId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_OrganizationalManagementId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ContentRevision",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OrganizationalActivityId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OrganizationalManagementId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Documents");

            migrationBuilder.DropSequence(
                name: "DocumentSerialSequence",
                schema: "dbo");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(18)",
                oldMaxLength: 18);
        }
    }
}
