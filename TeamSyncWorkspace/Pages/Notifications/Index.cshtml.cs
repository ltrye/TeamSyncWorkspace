using System.Collections.Generic;
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
    public class IndexModel : PageModel
    {
        private readonly NotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(
            NotificationService notificationService,
            UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public List<Notification> Notifications { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            Notifications = await _notificationService.GetUserNotificationsAsync(user.Id, 50);
            return Page();
        }

        public async Task<IActionResult> OnGetMarkAsReadAsync(int id)
        {
            await _notificationService.MarkNotificationAsReadAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetMarkAllAsReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await _notificationService.MarkAllNotificationsAsReadAsync(user.Id);
            StatusMessage = "All notifications marked as read.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetDeleteAsync(int id)
        {
            await _notificationService.DeleteNotificationAsync(id);
            StatusMessage = "Notification deleted.";
            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostMarkAsReadAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var notification = await _notificationService.GetAndMarkAsReadAsync(id);

            if (notification == null)
            {
                return NotFound();
            }

            if (notification.UserId != user.Id)
            {
                return Forbid();
            }

            return new JsonResult(new { success = true });
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostMarkAllAsReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            await _notificationService.MarkAllNotificationsAsReadAsync(user.Id);

            return new JsonResult(new { success = true });
        }
    }
}