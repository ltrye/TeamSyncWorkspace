using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class TeamService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TeamService> _logger;
        private readonly TeamRoleService _teamRoleService;

        public TeamService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<TeamService> logger,
            TeamRoleService teamRoleService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _teamRoleService = teamRoleService;
        }

        public async Task<(bool success, string message, Team team)> CreateTeamAsync(int userId, string teamName, string workspaceName = null)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(teamName))
            {
                return (false, "Team name cannot be empty.", null);
            }

            // Check if user exists
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found.", null);
            }

            // Check if team name already exists for this user
            var userTeams = await _context.TeamMembers
                .Where(tm => tm.UserId == userId)
                .Include(tm => tm.Team)
                .Select(tm => tm.Team)
                .ToListAsync();

            if (userTeams.Any(t => t.TeamName.Equals(teamName, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, "You already have a team with this name.", null);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Create new team
                var team = new Team
                {
                    TeamName = teamName,
                    CreatedDate = DateTime.Now
                };

                // Add team to database
                _context.Teams.Add(team);
                await _context.SaveChangesAsync();

                // Add user as team member with Owner role
                var teamMember = new TeamMember
                {
                    TeamId = team.TeamId,
                    UserId = userId,
                    JoinedDate = DateTime.Now,
                    Role = "Owner" // Set as Owner role
                };

                _context.TeamMembers.Add(teamMember);
                await _context.SaveChangesAsync();

                // Create workspace for the team with UUID
                var workspace = new Workspace
                {
                    WorkspaceId = Guid.NewGuid().ToString(),
                    TeamId = team.TeamId,
                    WorkspaceName = workspaceName ?? teamName, // Use team name as workspace name if not specified
                    Description = $"Workspace for {teamName}",
                    CreatedDate = DateTime.Now
                };

                _context.Workspaces.Add(workspace);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("User {UserId} created a new team {TeamName} with ID {TeamId} and workspace {WorkspaceId}",
                    userId, teamName, team.TeamId, workspace.WorkspaceId);

                return (true, "Team and workspace created successfully.", team);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating team and workspace");
                return (false, "An error occurred while creating the team and workspace.", null);
            }
        }

        public async Task<List<Team>> GetUserTeamsAsync(int userId)
        {
            return await _context.TeamMembers
                .Where(tm => tm.UserId == userId)
                .Include(tm => tm.Team)
                .Select(tm => tm.Team)
                .ToListAsync();
        }

        public async Task<(bool success, string message)> AddMemberToTeamAsync(int teamId, int userId, string role = "Member")
        {
            // Check if team exists
            var team = await _context.Teams.FindAsync(teamId);
            if (team == null)
            {
                return (false, "Team not found.");
            }

            // Check if user exists
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Check if role exists
            var roleExists = await _context.TeamRoles.AnyAsync(r => r.Name == role);
            if (!roleExists)
            {
                return (false, $"Role '{role}' does not exist.");
            }

            // Check if user is already a member
            var isAlreadyMember = await _context.TeamMembers
                .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);

            if (isAlreadyMember)
            {
                return (false, "User is already a member of this team.");
            }

            // Add the user to the team
            var teamMember = new TeamMember
            {
                TeamId = teamId,
                UserId = userId,
                JoinedDate = DateTime.Now,
                Role = role
            };

            _context.TeamMembers.Add(teamMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} added to team {TeamId} with role {Role}", userId, teamId, role);

            return (true, $"User added to team successfully with '{role}' role.");
        }

        public async Task<Team> GetTeamByIdAsync(int teamId)
        {
            return await _context.Teams.FindAsync(teamId);
        }

        public async Task<List<TeamMember>> GetTeamMembersAsync(int teamId)
        {
            return await _context.TeamMembers
                .Where(tm => tm.TeamId == teamId)
                .Include(tm => tm.User)
                .ToListAsync();
        }

        public async Task<bool> CanUserPerformActionAsync(int teamId, int userId, string action)
        {
            return await _teamRoleService.UserCanPerformActionAsync(teamId, userId, action);
        }

        public async Task<string> GetUserRoleInTeamAsync(int teamId, int userId)
        {
            var teamMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId);

            return teamMember?.Role;
        }

        public async Task<(bool success, string message)> UpdateMemberRoleAsync(int teamId, int userId, int targetUserId, string newRole)
        {
            // Check if the user performing the action has permission
            bool canManageRoles = await _teamRoleService.UserCanPerformActionAsync(
                teamId, userId, ActivityType.ManageRoles);

            if (!canManageRoles)
            {
                return (false, "You don't have permission to manage roles.");
            }

            // Check if role exists
            var roleExists = await _context.TeamRoles.AnyAsync(r => r.Name == newRole);
            if (!roleExists)
            {
                return (false, $"Role '{newRole}' does not exist.");
            }

            // Get the team member to update
            var teamMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == targetUserId);

            if (teamMember == null)
            {
                return (false, "Team member not found.");
            }

            // Don't allow changing the Owner's role
            if (teamMember.Role == "Owner" && userId != targetUserId)
            {
                return (false, "Cannot change the role of the team owner.");
            }

            // Update the role
            teamMember.Role = newRole;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated role of user {TargetUserId} to {Role} in team {TeamId}",
                userId, targetUserId, newRole, teamId);

            return (true, $"Role updated to '{newRole}' successfully.");
        }

        public async Task<bool> IsUserTeamMemberAsync(int teamId, int userId)
        {
            return await _context.TeamMembers
                .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);
        }

        public async Task<(bool success, string message)> RemoveMemberFromTeamAsync(int teamId, int userId, int actingUserId)
        {
            // Check if team exists
            var team = await _context.Teams.FindAsync(teamId);
            if (team == null)
            {
                return (false, "Team not found.");
            }

            // Check if user exists
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Check if user has permission to remove members
            bool canRemoveMembers = await _teamRoleService.UserCanPerformActionAsync(
                teamId, actingUserId, ActivityType.RemoveMembers);

            if (!canRemoveMembers)
            {
                return (false, "You don't have permission to remove team members.");
            }

            // Check if user is a member of the team
            var teamMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId);

            if (teamMember == null)
            {
                return (false, "User is not a member of this team.");
            }

            // Don't allow removing the owner
            if (teamMember.Role == "Owner")
            {
                return (false, "Cannot remove the team owner.");
            }

            // Remove the user from the team
            _context.TeamMembers.Remove(teamMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {ActingUserId} removed user {UserId} from team {TeamId}",
                actingUserId, userId, teamId);

            return (true, $"User removed from team successfully.");
        }

        public async Task<bool> IsUserTeamAdminAsync(int teamId, int id)
        {
            return await _context.TeamMembers
                 .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == id && tm.Role == "Admin");
        }

        public async Task<List<Chat>> GetGroupChatsByTeamIdAsync(int teamId, int userId)
        {

            var teamMemberIds = await _context.TeamMembers
                .Where(tm => tm.TeamId == teamId && tm.UserId == userId)
                .Select(tm => tm.UserId)
                .ToListAsync();

            return await _context.Chats
                .Where(c => c.IsGroup && c.ChatMembers.Any(cm => teamMemberIds.Contains(cm.UserId)))
                .ToListAsync();
        }
    }
}