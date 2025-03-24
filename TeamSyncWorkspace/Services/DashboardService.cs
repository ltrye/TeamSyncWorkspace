using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class DashboardService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly InvitationService _invitationService;

        public DashboardService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            InvitationService invitationService)
        {
            _context = context;
            _userManager = userManager;
            _invitationService = invitationService;
        }

        public async Task<ApplicationUser> GetCurrentUserAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task<Team> GetTeamAsync(int teamId)
        {
            return await _context.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamId);
        }

        public async Task<bool> IsUserTeamMemberAsync(int teamId, int userId)
        {
            return await _context.TeamMembers
                .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);
        }

        public async Task<List<TeamMember>> GetTeamMembersAsync(int teamId)
        {
            return await _context.TeamMembers
                .Include(tm => tm.User)
                .Where(tm => tm.TeamId == teamId)
                .ToListAsync();
        }

        public async Task<Workspace> GetTeamWorkspaceAsync(int teamId)
        {
            return await _context.Workspaces
                .FirstOrDefaultAsync(w => w.TeamId == teamId);
        }

        public bool IsTeamAdmin(List<TeamMember> teamMembers, int userId)
        {
            // In a real app, you would have roles or a specific flag
            // This is a simplified implementation
            var firstMember = teamMembers.FirstOrDefault();
            return firstMember != null && firstMember.UserId == userId;
        }

        public async Task<(bool success, string message)> InviteUserToTeamAsync(string email, int teamId, int invitedByUserId)
        {
            // Check if email is valid
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email address cannot be empty.");
            }

            // Delegate to the invitation service
            var (success, message, _) = await _invitationService.CreateInvitationAsync(teamId, invitedByUserId, email);
            return (success, message);
        }

        public async Task<(bool success, string message)> UpdateWorkspaceAsync(string workspaceId, string workspaceName, string description)
        {
            var workspace = await _context.Workspaces.FindAsync(workspaceId);

            if (workspace == null)
            {
                return (false, "Workspace not found.");
            }

            workspace.WorkspaceName = workspaceName;
            workspace.Description = description;

            await _context.SaveChangesAsync();

            return (true, "Workspace updated successfully.");
        }
    }
}