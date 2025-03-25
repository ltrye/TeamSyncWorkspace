using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Dashboard
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly DashboardService _dashboardService;
        private readonly InvitationService _invitationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TeamService _teamService;

        public IndexModel(
            DashboardService dashboardService,
            InvitationService invitationService,
            TeamService teamService,
            UserManager<ApplicationUser> userManager)
        {
            _dashboardService = dashboardService;
            _invitationService = invitationService;
            _teamService = teamService;
            _userManager = userManager;
        }

        public Team Team { get; set; }
        public List<TeamMember> TeamMembers { get; set; }
        public ApplicationUser CurrentUser { get; set; }
        public bool IsTeamAdmin { get; set; }
        public Workspace Workspace { get; set; }

        [BindProperty]
        public string? InviteEmail { get; set; }


        public CreateWorkspaceInputModel? CreateWorkspaceInput { get; set; }


        public Workspace? WorkspaceUpdate { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class CreateWorkspaceInputModel
        {
            public string WorkspaceName { get; set; }
            public string WorkspaceDescription { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int teamId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get team information
            Team = await _dashboardService.GetTeamAsync(teamId);
            if (Team == null)
            {
                return NotFound($"Unable to load team with ID '{teamId}'.");
            }

            // Check if user is a member of the team
            var isMember = await _dashboardService.IsUserTeamMemberAsync(teamId, user.Id);
            if (!isMember)
            {
                return Forbid();
            }

            // Get team members
            TeamMembers = await _dashboardService.GetTeamMembersAsync(teamId);

            // Get the single workspace for this team
            Workspace = await _dashboardService.GetTeamWorkspaceAsync(teamId);

            // Determine if user is admin
            // Using the synchronous IsTeamAdmin method that's available in DashboardService
            IsTeamAdmin = _dashboardService.IsTeamAdmin(TeamMembers, user.Id);

            return Page();
        }

        // Update the OnPostInviteAsync method
        public async Task<IActionResult> OnPostInviteAsync(int teamId)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Invalid input");
                return await OnGetAsync(teamId);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message, _) = await _invitationService.CreateInvitationAsync(teamId, user.Id, InviteEmail);

            StatusMessage = message;

            return RedirectToPage("./Index", new { teamId, message = "Send invite link success" });
        }

        public async Task<IActionResult> OnPostUpdateWorkspaceAsync(int teamId, string workspaceId, string workspaceName, string description)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get team members to check if user is admin
            var teamMembers = await _dashboardService.GetTeamMembersAsync(teamId);

            // Check if user is admin of the team (using the synchronous method)
            var isAdmin = _dashboardService.IsTeamAdmin(teamMembers, user.Id);
            if (!isAdmin)
            {
                StatusMessage = "Error: You don't have permission to update this workspace.";
                return RedirectToPage(new { teamId });
            }

            var (success, message) = await _dashboardService.UpdateWorkspaceAsync(workspaceId, workspaceName, description);



            StatusMessage = message;

            return RedirectToPage(new { teamId });
        }

        // Add this helper method to your IndexModel class in Dashboard/Index.cshtml.cs
        public async Task<bool> CanUserPerformAction(string action)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            return await _teamService.CanUserPerformActionAsync(Team.TeamId, user.Id, action);
        }
    }
}