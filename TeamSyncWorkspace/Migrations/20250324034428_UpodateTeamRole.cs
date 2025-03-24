using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSyncWorkspace.Migrations
{
    /// <inheritdoc />
    public partial class UpodateTeamRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "TeamRoles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamRoles_TeamId",
                table: "TeamRoles",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamRoles_Teams_TeamId",
                table: "TeamRoles",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamRoles_Teams_TeamId",
                table: "TeamRoles");

            migrationBuilder.DropIndex(
                name: "IX_TeamRoles_TeamId",
                table: "TeamRoles");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "TeamRoles");
        }
    }
}
