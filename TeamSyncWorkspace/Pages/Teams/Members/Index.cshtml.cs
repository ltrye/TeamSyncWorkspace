using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Teams.Members
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly TeamService _teamService;
        private readonly TeamRoleService _teamRoleService;
        private readonly TeamRoleManagementService _roleManagementService;
        private readonly InvitationService _invitationService;
        private readonly NotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            TeamService teamService,
            TeamRoleService teamRoleService,
            TeamRoleManagementService roleManagementService,
            InvitationService invitationService,
            NotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            ILogger<IndexModel> logger)
        {
            _teamService = teamService;
            _teamRoleService = teamRoleService;
            _roleManagementService = roleManagementService;
            _invitationService = invitationService;
            _notificationService = notificationService;
            _userManager = userManager;
            _logger = logger;
        }

        public Team Team { get; set; }
        public List<TeamMember> TeamMembers { get; set; }
        public List<TeamRole> Roles { get; set; }
        public bool CanManageRoles { get; set; }
        public bool CanInviteMembers { get; set; }
        public bool CanRemoveMembers { get; set; }
        public int CurrentUserId { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int teamId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            CurrentUserId = user.Id;

            // Get team information
            Team = await _teamService.GetTeamByIdAsync(teamId);
            if (Team == null)
            {
                return NotFound($"Unable to load team with ID '{teamId}'.");
            }

            // Check if user is a member of the team
            var isMember = await _teamService.IsUserTeamMemberAsync(teamId, user.Id);
            if (!isMember)
            {
                return Forbid();
            }

            // Get all roles
            Roles = await _teamRoleService.GetTeamSpecificRolesAsync(Team.TeamId);

            // Get team members
            TeamMembers = await _teamService.GetTeamMembersAsync(teamId);

            // Check user permissions
            CanManageRoles = await _teamRoleService.UserCanPerformActionAsync(teamId, user.Id, ActivityType.ManageRoles);
            CanInviteMembers = await _teamRoleService.UserCanPerformActionAsync(teamId, user.Id, ActivityType.InviteMembers);
            CanRemoveMembers = await _teamRoleService.UserCanPerformActionAsync(teamId, user.Id, ActivityType.RemoveMembers);

            return Page();
        }

        public async Task<IActionResult> OnPostChangeRoleAsync(int teamId, int userId, string newRole)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message) = await _roleManagementService.ChangeTeamMemberRoleAsync(
                teamId, currentUser.Id, userId, newRole);

            StatusMessage = message;
            return RedirectToPage(new { teamId });
        }

        public async Task<IActionResult> OnPostInviteAsync(int teamId, string email, string role, string message = "")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Check if user can invite members
            bool canInvite = await _teamRoleService.UserCanPerformActionAsync(
                teamId, currentUser.Id, ActivityType.InviteMembers);

            if (!canInvite)
            {
                StatusMessage = "Error: You don't have permission to invite team members.";
                return RedirectToPage(new { teamId });
            }

            var (success, responseMessage, _) = await _invitationService.CreateInvitationAsync(
                teamId, currentUser.Id, email, role, message);

            StatusMessage = responseMessage;
            return RedirectToPage(new { teamId });
        }

        public async Task<IActionResult> OnPostRemoveMemberAsync(int teamId, int userId, string reason = "")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Check if user can remove members
            bool canRemove = await _teamRoleService.UserCanPerformActionAsync(
                teamId, currentUser.Id, ActivityType.RemoveMembers);

            if (!canRemove)
            {
                StatusMessage = "Error: You don't have permission to remove team members.";
                return RedirectToPage(new { teamId });
            }

            // Cannot remove yourself
            if (userId == currentUser.Id)
            {
                StatusMessage = "Error: You cannot remove yourself from the team.";
                return RedirectToPage(new { teamId });
            }

            // Cannot remove the owner
            var memberRole = await _teamService.GetUserRoleInTeamAsync(teamId, userId);
            if (memberRole == "Owner")
            {
                StatusMessage = "Error: You cannot remove the team owner.";
                return RedirectToPage(new { teamId });
            }

            // Get member information for notification
            var team = await _teamService.GetTeamByIdAsync(teamId);
            var member = await _userManager.FindByIdAsync(userId.ToString());

            if (team == null || member == null)
            {
                StatusMessage = "Error: Unable to find team or member information.";
                return RedirectToPage(new { teamId });
            }

            // Remove the member
            var (success, message) = await _teamService.RemoveMemberFromTeamAsync(teamId, userId, currentUser.Id);

            if (success)
            {
                // Send notification to the removed user
                string notificationTitle = $"Removed from {team.TeamName}";
                string notificationMessage = string.IsNullOrEmpty(reason)
                    ? $"You have been removed from the team {team.TeamName}."
                    : $"You have been removed from the team {team.TeamName}. Reason: {reason}";

                await _notificationService.CreateNotificationAsync(
                    userId,
                    notificationTitle,
                    notificationMessage,
                    "/Teams/Index",
                    "TeamMemberRemoval");
            }

            StatusMessage = message;
            return RedirectToPage(new { teamId });
        }
    }
}