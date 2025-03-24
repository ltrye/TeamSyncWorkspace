using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Shared.Components.Notifications
{
    public class NotificationsViewComponent : ViewComponent
    {
        private readonly NotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsViewComponent(
            NotificationService notificationService,
            UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return View("Default", new NotificationsViewModel
                {
                    Notifications = new List<Notification>(),
                    UnreadCount = 0
                });
            }

            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return View("Default", new NotificationsViewModel
                {
                    Notifications = new List<Notification>(),
                    UnreadCount = 0
                });
            }

            var notifications = await _notificationService.GetUserNotificationsAsync(user.Id, 5);
            var unreadCount = await _notificationService.GetUnreadNotificationCountAsync(user.Id);

            var viewModel = new NotificationsViewModel
            {
                Notifications = notifications,
                UnreadCount = unreadCount
            };

            return View("Default", viewModel);
        }
    }

    public class NotificationsViewModel
    {
        public List<Notification> Notifications { get; set; }
        public int UnreadCount { get; set; }
    }
}