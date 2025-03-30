using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSyncWorkspace.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToTimelineTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedId",
                table: "TimelineTasks",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedId",
                table: "TimelineTasks");
        }
    }
}
