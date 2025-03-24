using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(AppDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Notification> CreateNotificationAsync(
            int userId,
            string title,
            string message,
            string link = null,
            string type = "General",
            int? relatedEntityId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Link = link,
                CreatedDate = DateTime.Now,
                IsRead = false,
                Type = type,
                RelatedEntityId = relatedEntityId
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created notification for user {UserId}: {Title}", userId, title);

            return notification;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(int userId, int count = 10)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetUnreadNotificationCountAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();
        }

        public async Task MarkNotificationAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllNotificationsAsReadAsync(int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteNotificationAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Notification> GetAndMarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Notification {NotificationId} marked as read", notificationId);
            }
            return notification;
        }
    }
}