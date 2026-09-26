using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSEQ.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReservationExpiresAtUtc",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            // defaultValue: 3 = Published. عمداً به‌جای 0: همه‌ی اسنادِ موجود فایل دارند و
            // زنده‌اند، پس باید Published شوند - نه 0 که اصلاً وضعیتِ معتبری نیست. این مقدار
            // ردیف‌های موجود را هم پر می‌کند و هم default معتبری برای درج‌های خام می‌گذارد؛
            // EF خودش همیشه Status را صریح می‌فرستد، پس این default مسیرِ عادی را تغییر نمی‌دهد.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ReservationExpiresAtUtc",
                table: "Documents",
                column: "ReservationExpiresAtUtc",
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_ReservationExpiresAtUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ReservationExpiresAtUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Documents");
        }
    }
}
