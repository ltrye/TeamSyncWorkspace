using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Teams.Roles
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly TeamService _teamService;
        private readonly TeamRoleService _teamRoleService;
        private readonly TeamRoleManagementService _roleManagementService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            TeamService teamService,
            TeamRoleService teamRoleService,
            TeamRoleManagementService roleManagementService,
            UserManager<ApplicationUser> userManager,
            ILogger<IndexModel> logger)
        {
            _teamService = teamService;
            _teamRoleService = teamRoleService;
            _roleManagementService = roleManagementService;
            _userManager = userManager;
            _logger = logger;
        }

        public Team Team { get; set; }
        public List<TeamRole> Roles { get; set; }
        public List<TeamMember> TeamMembers { get; set; }
        public List<(string action, string description)> AvailablePermissions { get; set; }
        public bool CanManageRoles { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int teamId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

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

            // Check if user can manage roles
            CanManageRoles = await _teamRoleService.UserCanPerformActionAsync(teamId, user.Id, ActivityType.ManageRoles);

            // Get available permissions
            AvailablePermissions = _roleManagementService.GetAvailablePermissions();

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

        [BindProperty]
        public CustomRoleInputModel CustomRole { get; set; }

        public class CustomRoleInputModel
        {
            [Required]
            [StringLength(30, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 3)]
            public string Name { get; set; }

            [Required]
            [StringLength(100)]
            public string Description { get; set; }

            public List<string> Permissions { get; set; } = new List<string>();
        }

        public async Task<IActionResult> OnPostCreateRoleAsync(int teamId)
        {
            if (!ModelState.IsValid)
            {
                return await OnGetAsync(teamId);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message, _) = await _roleManagementService.CreateCustomRoleAsync(
                teamId,
                currentUser.Id,
                CustomRole.Name,
                CustomRole.Description,
                CustomRole.Permissions);

            StatusMessage = message;
            return RedirectToPage(new { teamId });
        }

        public async Task<IActionResult> OnPostUpdateRoleAsync(int teamId, int roleId)
        {
            if (!ModelState.IsValid)
            {
                return await OnGetAsync(teamId);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message) = await _roleManagementService.UpdateCustomRoleAsync(
                teamId,
                currentUser.Id,
                roleId,
                CustomRole.Name,
                CustomRole.Description,
                CustomRole.Permissions);

            StatusMessage = message;
            return RedirectToPage(new { teamId });
        }

        public async Task<IActionResult> OnPostDeleteRoleAsync(int teamId, int roleId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message) = await _roleManagementService.DeleteCustomRoleAsync(
                teamId, currentUser.Id, roleId);

            StatusMessage = message;
            return RedirectToPage(new { teamId });
        }
    }
}