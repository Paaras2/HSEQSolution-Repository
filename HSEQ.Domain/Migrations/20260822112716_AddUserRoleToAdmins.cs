using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSEQ.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleToAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Admins",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ردیف‌های موجود جدول Admins همگی «مدیر سیستم» بوده‌اند (پیش از این ستون،
            // وجودِ ردیف یعنی مدیر سیستم). بدون این، همه با نقش نامعتبر 0 می‌ماندند.
            migrationBuilder.Sql("UPDATE [Admins] SET [Role] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Pcode",
                table: "Admins",
                column: "Pcode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Admins_Pcode",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Admins");
        }
    }
}
