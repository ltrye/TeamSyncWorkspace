using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class TeamRoleService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TeamRoleService> _logger;

        public TeamRoleService(AppDbContext context, ILogger<TeamRoleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeDefaultRolesAsync()
        {
            // Check if roles already exist
            if (await _context.TeamRoles.AnyAsync())
            {
                return; // Roles already initialized
            }

            // Define default roles
            var ownerRole = new TeamRole
            {
                Name = "Owner",
                Description = "Team creator with full control over all team settings and data"
            };

            var adminRole = new TeamRole
            {
                Name = "Admin",
                Description = "Can manage team members and most team settings"
            };

            var memberRole = new TeamRole
            {
                Name = "Member",
                Description = "Regular team member with standard permissions"
            };

            var viewerRole = new TeamRole
            {
                Name = "Viewer",
                Description = "Can only view content without making changes"
            };

            // Add roles to database
            await _context.TeamRoles.AddRangeAsync(ownerRole, adminRole, memberRole, viewerRole);
            await _context.SaveChangesAsync();

            // Define permissions for Owner (all permissions)
            var ownerPermissions = new List<TeamRolePermission>();
            foreach (var field in typeof(ActivityType).GetFields())
            {
                if (field.IsLiteral && !field.IsInitOnly)
                {
                    ownerPermissions.Add(new TeamRolePermission
                    {
                        RoleId = ownerRole.RoleId,
                        Action = field.GetValue(null).ToString()
                    });
                }
            }

            // Define permissions for Admin (most permissions except delete team)
            var adminPermissions = new List<TeamRolePermission>();
            foreach (var field in typeof(ActivityType).GetFields())
            {
                if (field.IsLiteral && !field.IsInitOnly)
                {
                    var action = field.GetValue(null).ToString();
                    if (action != ActivityType.DeleteTeam)
                    {
                        adminPermissions.Add(new TeamRolePermission
                        {
                            RoleId = adminRole.RoleId,
                            Action = action
                        });
                    }
                }
            }

            // Define permissions for Member (basic content creation and editing)
            var memberPermissions = new List<TeamRolePermission>
            {
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.ViewTeam },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.CreateWorkspace },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.EditWorkspace },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.CreateDocument },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.EditDocument },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.UploadFile },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.DownloadFile },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.CreateTask },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.EditTask },
                new TeamRolePermission { RoleId = memberRole.RoleId, Action = ActivityType.CompleteTask }
            };

            // Define permissions for Viewer (view-only)
            var viewerPermissions = new List<TeamRolePermission>
            {
                new TeamRolePermission { RoleId = viewerRole.RoleId, Action = ActivityType.ViewTeam },
                new TeamRolePermission { RoleId = viewerRole.RoleId, Action = ActivityType.DownloadFile }
            };

            // Add all permissions to database
            await _context.TeamRolePermissions.AddRangeAsync(
                ownerPermissions.Concat(adminPermissions)
                                .Concat(memberPermissions)
                                .Concat(viewerPermissions)
            );
            await _context.SaveChangesAsync();

            _logger.LogInformation("Default team roles and permissions initialized");
        }

        public async Task<bool> UserCanPerformActionAsync(int teamId, int userId, string action)
        {
            // Get the user's role in the team
            var teamMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId);

            if (teamMember == null)
            {
                return false; // User is not a member of the team
            }

            // Find the role assigned to the user
            var role = await _context.TeamRoles
                .FirstOrDefaultAsync(r => r.Name == teamMember.Role);

            if (role == null)
            {
                return false; // Role not found
            }

            // Check if the role has permission for the requested action
            return await _context.TeamRolePermissions
                .AnyAsync(p => p.RoleId == role.RoleId && p.Action == action);
        }

        public async Task<List<string>> GetUserPermissionsAsync(int teamId, int userId)
        {
            // Get the user's role in the team
            var teamMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId);

            if (teamMember == null)
            {
                return new List<string>(); // User is not a member of the team
            }

            // Find the role assigned to the user
            var role = await _context.TeamRoles
                .FirstOrDefaultAsync(r => r.Name == teamMember.Role);

            if (role == null)
            {
                return new List<string>(); // Role not found
            }

            // Get all permissions for the role
            return await _context.TeamRolePermissions
                .Where(p => p.RoleId == role.RoleId)
                .Select(p => p.Action)
                .ToListAsync();
        }

        public async Task<List<TeamRole>> GetAllRolesAsync()
        {
            return await _context.TeamRoles
                .Include(r => r.Permissions)
                .ToListAsync();
        }

        public async Task<List<TeamRole>> GetTeamSpecificRolesAsync(int teamId)
        {
            return await _context.TeamRoles
                .Where(r => r.TeamId == teamId || r.TeamId == null)
                .Include(r => r.Permissions)
                .ToListAsync();
        }

        public async Task<TeamRole> GetRoleByNameAsync(string roleName)
        {
            return await _context.TeamRoles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Name == roleName);
        }
    }
}