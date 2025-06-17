using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PFE_Back.Migrations
{
    /// <inheritdoc />
    public partial class AddLastLoginDateColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Accuracy",
                table: "Documents",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ProcessingTime",
                table: "Documents",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Accuracy",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProcessingTime",
                table: "Documents");
        }
    }
}
