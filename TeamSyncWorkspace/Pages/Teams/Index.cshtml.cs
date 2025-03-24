using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Teams
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly TeamService _teamService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            TeamService teamService,
            UserManager<ApplicationUser> userManager,
            ILogger<CreateModel> logger)
        {
            _teamService = teamService;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public List<Team> UserTeams { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(50, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 3)]
            [Display(Name = "Team Name")]
            public string TeamName { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get user's teams to display on the page
            UserTeams = await _teamService.GetUserTeamsAsync(user.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message, team) = await _teamService.CreateTeamAsync(user.Id, Input.TeamName);

            StatusMessage = message;

            if (success && team != null)
            {
                // Redirect to the team dashboard
                return RedirectToPage("/Dashboard/Index", new { teamId = team.TeamId });
            }

            // Get user's teams to display on the page
            UserTeams = await _teamService.GetUserTeamsAsync(user.Id);

            return Page();
        }
    }
}