using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSEQ.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadquartersDocumentNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // اسناد از قبل موجود، همه از مسیر قدیمی (فقط پروژه) ساخته شده‌اند. مقدار
            // پیش‌فرض ستون بالا (0 = ستاد) فقط برای اسناد جدید درست است؛ بدون این خط،
            // همه‌ی اسناد پروژه‌ی قبلی به‌اشتباه «ستاد» علامت می‌خوردند با اینکه ProjectId
            // و شماره‌ی PPPP-دار دارند.
            migrationBuilder.Sql("UPDATE [Documents] SET [Category] = 1 WHERE [ProjectId] IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "HeadquartersCodeCounters",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code5 = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    LastSerialNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadquartersCodeCounters", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "LegacyDocumentNumbers",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code5 = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    SerialNumber = table.Column<int>(type: "int", nullable: true),
                    RevisionSuffix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ManagementCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    ActivityCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnitLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastEditShamsiDate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CurrentEditShamsiDate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CurrentVersion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyDocumentNumbers", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeadquartersCodeCounters_Code5",
                table: "HeadquartersCodeCounters",
                column: "Code5",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyDocumentNumbers_Code5",
                table: "LegacyDocumentNumbers",
                column: "Code5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeadquartersCodeCounters");

            migrationBuilder.DropTable(
                name: "LegacyDocumentNumbers");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Documents");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
