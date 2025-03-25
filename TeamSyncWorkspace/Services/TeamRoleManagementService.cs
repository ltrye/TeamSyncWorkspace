using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class TeamRoleManagementService
    {
        #region Constants

        // Role constants
        public static class Roles
        {
            public const string Owner = "Owner";
            public const string Admin = "Admin";
            public const string Member = "Member";
            public const string Viewer = "Viewer";

            // List of built-in roles that cannot be modified or deleted
            public static readonly string[] BuiltInRoles = { Owner, Admin, Member, Viewer };
        }

        #endregion

        #region Constructor and Dependencies

        private readonly AppDbContext _context;
        private readonly TeamRoleService _teamRoleService;
        private readonly TeamService _teamService;
        private readonly ILogger<TeamRoleManagementService> _logger;

        public TeamRoleManagementService(
            AppDbContext context,
            TeamRoleService teamRoleService,
            TeamService teamService,
            ILogger<TeamRoleManagementService> logger)
        {
            _context = context;
            _teamRoleService = teamRoleService;
            _teamService = teamService;
            _logger = logger;
        }

        #endregion

        #region Change Member Roles

        /// <summary>
        /// Change a team member's role
        /// </summary>
        public async Task<(bool success, string message)> ChangeTeamMemberRoleAsync(int teamId, int actingUserId, int targetUserId, string newRoleName)
        {
            // Verify the acting user has permission to manage roles
            bool canManageRoles = await _teamRoleService.UserCanPerformActionAsync(
                teamId, actingUserId, ActivityType.ManageRoles);

            if (!canManageRoles)
            {
                return (false, "You don't have permission to manage roles in this team.");
            }

            // Verify the role exists
            var role = await _teamRoleService.GetRoleByNameAsync(newRoleName);
            if (role == null)
            {
                return (false, $"Role '{newRoleName}' does not exist.");
            }

            // Get the target user's team membership
            var teamMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == targetUserId);

            if (teamMember == null)
            {
                return (false, "User is not a member of this team.");
            }

            // Don't allow changing the Owner's role by anyone except the Owner themselves
            if (teamMember.Role == Roles.Owner && actingUserId != targetUserId)
            {
                return (false, "Only the team owner can change their own role.");
            }

            // Don't allow regular admins to change other admins' roles
            var actingUserRole = await _teamService.GetUserRoleInTeamAsync(teamId, actingUserId);
            if (actingUserRole == Roles.Admin && teamMember.Role == Roles.Admin && actingUserId != targetUserId)
            {
                return (false, "Admins cannot change the role of other admins.");
            }

            // Don't allow non-owners to assign the Owner role
            if (newRoleName == Roles.Owner && actingUserRole != Roles.Owner)
            {
                return (false, "Only the current owner can transfer ownership.");
            }

            // Check if acting user is the only owner and trying to change their role
            if (teamMember.Role == Roles.Owner && actingUserId == targetUserId && newRoleName != Roles.Owner)
            {
                // Count number of owners in the team
                int ownerCount = await _context.TeamMembers
                    .CountAsync(tm => tm.TeamId == teamId && tm.Role == Roles.Owner);

                if (ownerCount <= 1)
                {
                    return (false, "Cannot change role: team must have at least one owner.");
                }
            }

            // If transferring ownership, change the old owner to Admin
            if (newRoleName == Roles.Owner && actingUserRole == Roles.Owner && actingUserId != targetUserId)
            {
                var currentOwner = await _context.TeamMembers
                    .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == actingUserId);

                if (currentOwner != null)
                {
                    currentOwner.Role = Roles.Admin;
                }
            }

            // Update the role
            teamMember.Role = newRoleName;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {ActingUserId} changed role of user {TargetUserId} to {Role} in team {TeamId}",
                actingUserId, targetUserId, newRoleName, teamId);

            return (true, $"User role has been updated to '{newRoleName}' successfully.");
        }

        #endregion

        #region Create and Manage Custom Roles

        /// <summary>
        /// Create a custom role for a team with selected permissions
        /// </summary>
        public async Task<(bool success, string message, TeamRole? role)> CreateCustomRoleAsync(
            int teamId,
            int actingUserId,
            string roleName,
            string roleDescription,
            List<string> permissions)
        {
            // Verify the acting user has permission to manage roles
            bool canManageRoles = await _teamRoleService.UserCanPerformActionAsync(
                teamId, actingUserId, ActivityType.ManageRoles);

            if (!canManageRoles)
            {
                return (false, "You don't have permission to create custom roles.", null);
            }

            // Check if role name conflicts with a built-in role
            if (Roles.BuiltInRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            {
                return (false, $"Cannot create a role with the same name as a built-in role.", null);
            }

            // Check if role name already exists
            var roleExists = await _context.TeamRoles
                .AnyAsync(r => r.Name.ToLower() == roleName.ToLower());

            if (roleExists)
            {
                return (false, $"A role with the name '{roleName}' already exists.", null);
            }

            // Validate permissions
            foreach (var permission in permissions)
            {
                bool isValidPermission = typeof(ActivityType)
                    .GetFields()
                    .Where(f => f.IsLiteral && !f.IsInitOnly)
                    .Select(f => f.GetValue(null).ToString())
                    .Contains(permission);

                if (!isValidPermission)
                {
                    return (false, $"Invalid permission: {permission}", null);
                }
            }

            // Create the custom role
            var customRole = new TeamRole
            {
                Name = roleName,
                Description = roleDescription,
                TeamId = teamId  // Associate the role with the specific team
            };

            _context.TeamRoles.Add(customRole);
            await _context.SaveChangesAsync();

            // Create the permissions for the role
            var rolePermissions = permissions.Select(p => new TeamRolePermission
            {
                RoleId = customRole.RoleId,
                Action = p
            }).ToList();

            await _context.TeamRolePermissions.AddRangeAsync(rolePermissions);
            await _context.SaveChangesAsync();

            // Log the creation
            _logger.LogInformation("User {UserId} created a custom role '{RoleName}' with {PermissionCount} permissions",
                actingUserId, roleName, permissions.Count);

            return (true, $"Custom role '{roleName}' created successfully.", customRole);
        }

        /// <summary>
        /// Update an existing custom role's permissions
        /// </summary>
        public async Task<(bool success, string message)> UpdateCustomRoleAsync(
            int teamId,
            int actingUserId,
            int roleId,
            string roleName,
            string roleDescription,
            List<string> permissions)
        {
            // Verify the acting user has permission to manage roles
            bool canManageRoles = await _teamRoleService.UserCanPerformActionAsync(
                teamId, actingUserId, ActivityType.ManageRoles);

            if (!canManageRoles)
            {
                return (false, "You don't have permission to update roles.");
            }

            // Get the role to update
            var role = await _context.TeamRoles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.RoleId == roleId);

            if (role == null)
            {
                return (false, "Role not found.");
            }

            // Don't allow updating built-in roles
            if (Roles.BuiltInRoles.Contains(role.Name))
            {
                return (false, "Built-in roles cannot be modified.");
            }

            // Check if new role name conflicts with a built-in role
            if (role.Name != roleName && Roles.BuiltInRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            {
                return (false, $"Cannot use a built-in role name for custom roles.");
            }

            // Check if new role name conflicts with existing one (if name is being changed)
            if (role.Name != roleName)
            {
                var nameConflict = await _context.TeamRoles
                    .AnyAsync(r => r.RoleId != roleId &&
                                  r.Name.ToLower() == roleName.ToLower());

                if (nameConflict)
                {
                    return (false, $"A role with the name '{roleName}' already exists.");
                }
            }

            // Validate permissions
            foreach (var permission in permissions)
            {
                bool isValidPermission = typeof(ActivityType)
                    .GetFields()
                    .Where(f => f.IsLiteral && !f.IsInitOnly)
                    .Select(f => f.GetValue(null).ToString())
                    .Contains(permission);

                if (!isValidPermission)
                {
                    return (false, $"Invalid permission: {permission}");
                }
            }

            // Update the role
            role.Name = roleName;
            role.Description = roleDescription;

            // Delete all existing permissions
            _context.TeamRolePermissions.RemoveRange(role.Permissions);

            // Add new permissions
            var newPermissions = permissions.Select(p => new TeamRolePermission
            {
                RoleId = role.RoleId,
                Action = p
            }).ToList();

            await _context.TeamRolePermissions.AddRangeAsync(newPermissions);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated role '{RoleName}' with {PermissionCount} permissions",
                actingUserId, roleName, permissions.Count);

            return (true, $"Role '{roleName}' updated successfully.");
        }

        /// <summary>
        /// Delete a custom role
        /// </summary>
        public async Task<(bool success, string message)> DeleteCustomRoleAsync(int teamId, int actingUserId, int roleId)
        {
            // Verify the acting user has permission to manage roles
            bool canManageRoles = await _teamRoleService.UserCanPerformActionAsync(
                teamId, actingUserId, ActivityType.ManageRoles);

            if (!canManageRoles)
            {
                return (false, "You don't have permission to delete roles.");
            }

            // Get the role to delete
            var role = await _context.TeamRoles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.RoleId == roleId);

            if (role == null)
            {
                return (false, "Role not found.");
            }

            // Don't allow deleting built-in roles
            if (Roles.BuiltInRoles.Contains(role.Name))
            {
                return (false, "Built-in roles cannot be deleted.");
            }

            // Check if any team members are using this role
            var membersWithRole = await _context.TeamMembers
                .AnyAsync(tm => tm.Role == role.Name);

            if (membersWithRole)
            {
                return (false, "Cannot delete a role that is assigned to team members. Reassign members first.");
            }

            // Delete the role permissions
            _context.TeamRolePermissions.RemoveRange(role.Permissions);

            // Delete the role
            _context.TeamRoles.Remove(role);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted role '{RoleName}'", actingUserId, role.Name);

            return (true, $"Role '{role.Name}' deleted successfully.");
        }

        #endregion

        #region Permission Management

        /// <summary>
        /// Get available permissions for creating/updating roles
        /// </summary>
        public List<(string action, string description)> GetAvailablePermissions()
        {
            // Create a list of permissions with descriptions
            var permissions = new List<(string action, string description)>
            {
                (ActivityType.ViewTeam, "View team details and members"),
                (ActivityType.ManageTeam, "Update team settings"),
                (ActivityType.DeleteTeam, "Delete the team permanently"),
                (ActivityType.InviteMembers, "Invite new members to the team"),
                (ActivityType.RemoveMembers, "Remove members from the team"),
                (ActivityType.ManageRoles, "Manage team roles and permissions"),
                (ActivityType.CreateWorkspace, "Create workspaces"),
                (ActivityType.EditWorkspace, "Edit workspace settings"),
                (ActivityType.DeleteWorkspace, "Delete workspaces"),
                (ActivityType.CreateDocument, "Create new documents"),
                (ActivityType.EditDocument, "Edit existing documents"),
                (ActivityType.DeleteDocument, "Delete documents"),
                (ActivityType.UploadFile, "Upload files to the workspace"),
                (ActivityType.DownloadFile, "Download files from the workspace"),
                (ActivityType.DeleteFile, "Delete files from the workspace"),
                (ActivityType.CreateTask, "Create new tasks"),
                (ActivityType.EditTask, "Edit existing tasks"),
                (ActivityType.AssignTask, "Assign tasks to team members"),
                (ActivityType.CompleteTask, "Mark tasks as complete"),
                (ActivityType.DeleteTask, "Delete tasks")
            };

            return permissions;
        }

        #endregion
    }
}