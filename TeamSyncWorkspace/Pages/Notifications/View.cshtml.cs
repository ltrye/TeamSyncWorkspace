using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Notifications
{
    [Authorize]
    public class ViewModel : PageModel
    {
        private readonly NotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ViewModel(
            NotificationService notificationService,
            UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public Notification Notification { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get and mark notification as read
            Notification = await _notificationService.GetAndMarkAsReadAsync(id);

            if (Notification == null)
            {
                return NotFound("Notification not found.");
            }

            if (Notification.UserId != user.Id)
            {
                return Forbid("You don't have permission to view this notification.");
            }

            // If notification has a link, redirect to it
            if (!string.IsNullOrEmpty(Notification.Link))
            {
                return Redirect(Notification.Link);
            }

            // Otherwise show the notification detail
            return Page();
        }
    }
}