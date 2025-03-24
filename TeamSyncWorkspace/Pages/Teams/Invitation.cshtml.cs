using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Teams
{
    [Authorize]
    public class InvitationModel : PageModel
    {
        private readonly InvitationService _invitationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<InvitationModel> _logger;

        public InvitationModel(
            InvitationService invitationService,
            UserManager<ApplicationUser> userManager,
            ILogger<InvitationModel> logger)
        {
            _invitationService = invitationService;
            _userManager = userManager;
            _logger = logger;
        }

        public TeamInvitation Invitation { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public bool IsExpired { get; set; }

        public bool IsProcessed { get; set; }

        public async Task<IActionResult> OnGetAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Teams/Index");
            }

            Invitation = await _invitationService.GetInvitationByTokenAsync(token);

            if (Invitation == null)
            {
                StatusMessage = "Invitation not found or has expired.";
                return RedirectToPage("/Teams/Index");
            }

            IsExpired = Invitation.ExpiryDate < DateTime.Now;
            IsProcessed = Invitation.IsAccepted || Invitation.IsDeclined;

            return Page();
        }

        public async Task<IActionResult> OnPostAcceptAsync(string token)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message) = await _invitationService.AcceptInvitationAsync(token, user.Id);

            StatusMessage = message;

            if (success)
            {
                // Get the teamId from the invitation for redirection
                var invitation = await _invitationService.GetInvitationByTokenAsync(token);
                return RedirectToPage("/Dashboard/Index", new { teamId = invitation.TeamId });
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeclineAsync(string token)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var (success, message) = await _invitationService.DeclineInvitationAsync(token, user.Id);

            StatusMessage = message;

            return RedirectToPage("/Teams/Index");
        }
    }
}