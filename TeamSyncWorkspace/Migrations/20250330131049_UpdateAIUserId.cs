using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSyncWorkspace.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAIUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "FirstName", "LastName", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "ProfileImageUrl", "RoleId", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { -1, 0, "70050bb1-c4ed-4408-8c80-60cc84588a56", "ai@assistant.com", true, "AI", "Assistant", false, null, "AI@ASSISTANT.COM", "AI ASSISTANT", null, null, false, null, 0, "5b490a21-eb6d-452a-a992-16efd3c4c2a5", false, "AI Assistant" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: -1);
        }
    }
}
