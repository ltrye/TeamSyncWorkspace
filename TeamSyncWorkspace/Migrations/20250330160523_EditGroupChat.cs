using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSyncWorkspace.Migrations
{
    /// <inheritdoc />
    public partial class EditGroupChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "Chats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "ChatMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "ChatMembers");
        }
    }
}
