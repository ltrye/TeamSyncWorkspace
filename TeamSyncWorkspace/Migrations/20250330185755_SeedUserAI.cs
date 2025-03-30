using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamSyncWorkspace.Migrations
{
    /// <inheritdoc />
    public partial class SeedUserAI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert the AI user if it does not already exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = -1)
                BEGIN
                    SET IDENTITY_INSERT AspNetUsers ON;
                    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, FirstName, LastName, SecurityStamp, ConcurrencyStamp, AccessFailedCount, LockoutEnabled, PhoneNumberConfirmed, TwoFactorEnabled, RoleId)
                    VALUES (-1, 'AI Assistant', 'AI ASSISTANT', 'ai@assistant.com', 'AI@ASSISTANT.COM', 1, 'AI', 'Assistant', 'f515c60d-85f1-44d3-ac43-b90639297fbd', 'f7e41779-07ae-49f8-9758-6d2309766f80', 0, 0, 0, 0, 0);
                    SET IDENTITY_INSERT AspNetUsers OFF;
                END
                ELSE
                BEGIN
                    UPDATE AspNetUsers
                    SET ConcurrencyStamp = 'f7e41779-07ae-49f8-9758-6d2309766f80', SecurityStamp = 'f515c60d-85f1-44d3-ac43-b90639297fbd'
                    WHERE Id = -1;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Optionally, you can delete the AI user in the Down method
            migrationBuilder.Sql(@"
                DELETE FROM AspNetUsers WHERE Id = -1;
            ");
        }
    }
}
