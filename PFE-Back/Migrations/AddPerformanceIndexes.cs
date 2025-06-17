using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PFE_Back.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add indexes for frequently queried columns
            
            // Documents table indexes
            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId_IsDeleted",
                table: "Documents",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status",
                table: "Documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Language",
                table: "Documents",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedAt",
                table: "Documents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FileName",
                table: "Documents",
                column: "FileName");

            // ActivityLogs table indexes
            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId_Timestamp",
                table: "ActivityLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Timestamp",
                table: "ActivityLogs",
                column: "Timestamp");

            // DocumentShares table indexes
            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_SharedWithEmail",
                table: "DocumentShares",
                column: "SharedWithEmail");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_DocumentId_IsActive",
                table: "DocumentShares",
                columns: new[] { "DocumentId", "IsActive" });

            // RefreshTokens table indexes
            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked",
                table: "RefreshTokens",
                columns: new[] { "UserId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiryDate",
                table: "RefreshTokens",
                column: "ExpiryDate");

            // DocumentProcessingHistory table indexes
            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistory_DocumentId_ProcessedAt",
                table: "DocumentProcessingHistory",
                columns: new[] { "DocumentId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessingHistory_UserId_ProcessedAt",
                table: "DocumentProcessingHistory",
                columns: new[] { "UserId", "ProcessedAt" });

            // AspNetUsers table indexes for soft delete
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsDeleted",
                table: "AspNetUsers",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email_IsDeleted",
                table: "AspNetUsers",
                columns: new[] { "Email", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all the indexes we created
            migrationBuilder.DropIndex(
                name: "IX_Documents_UserId_IsDeleted",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_Status",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_Language",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CreatedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_FileName",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_UserId_Timestamp",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_Timestamp",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_DocumentShares_SharedWithEmail",
                table: "DocumentShares");

            migrationBuilder.DropIndex(
                name: "IX_DocumentShares_DocumentId_IsActive",
                table: "DocumentShares");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ExpiryDate",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistory_DocumentId_ProcessedAt",
                table: "DocumentProcessingHistory");

            migrationBuilder.DropIndex(
                name: "IX_DocumentProcessingHistory_UserId_ProcessedAt",
                table: "DocumentProcessingHistory");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IsDeleted",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Email_IsDeleted",
                table: "AspNetUsers");
        }
    }
}
