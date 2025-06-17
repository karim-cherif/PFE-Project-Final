using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PFE_Back.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingForeignKeyRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OcrStatistics_UserId",
                table: "OcrStatistics",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_AspNetUsers_UserId",
                table: "ActivityLogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OcrStatistics_AspNetUsers_UserId",
                table: "OcrStatistics",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_AspNetUsers_UserId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_OcrStatistics_AspNetUsers_UserId",
                table: "OcrStatistics");

            migrationBuilder.DropIndex(
                name: "IX_OcrStatistics_UserId",
                table: "OcrStatistics");
        }
    }
}
