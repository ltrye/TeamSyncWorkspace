using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationsApiController : ControllerBase
    {
        private readonly NotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsApiController(
            NotificationService notificationService,
            UserManager<ApplicationUser> _userManager)
        {
            _notificationService = notificationService;
            this._userManager = _userManager;
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentNotifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(user.Id, 5);
            var unreadCount = await _notificationService.GetUnreadNotificationCountAsync(user.Id);

            return Ok(new { notifications, unreadCount });
        }

        [HttpPost("mark-read/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notification = await _notificationService.GetAndMarkAsReadAsync(id);

            if (notification == null) return NotFound();

            if (notification.UserId != user.Id) return Forbid();

            return Ok();
        }

        [HttpPost("mark-all-read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _notificationService.MarkAllNotificationsAsReadAsync(user.Id);

            return Ok();
        }
    }
}