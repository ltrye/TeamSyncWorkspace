using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSyncWorkspace.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIniteMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomMessage",
                table: "TeamInvitations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomMessage",
                table: "TeamInvitations");
        }
    }
}
