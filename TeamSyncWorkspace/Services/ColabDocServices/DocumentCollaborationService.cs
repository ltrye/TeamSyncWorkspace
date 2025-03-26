using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TeamSyncWorkspace.Services.ColabDocServices
{
    public class DocumentCollaborationService
    {
        private readonly ILogger<DocumentCollaborationService> _logger;
        private static readonly ConcurrentDictionary<string, List<object>> _activeUsers = new ConcurrentDictionary<string, List<object>>();

        public DocumentCollaborationService(ILogger<DocumentCollaborationService> logger)
        {
            _logger = logger;
        }

        public List<object> GetActiveUsers(int documentId)
        {
            string documentKey = GetDocumentKey(documentId);
            return _activeUsers.GetValueOrDefault(documentKey, new List<object>());
        }

        public bool AddUser(int documentId, object userInfo)
        {
            string documentKey = GetDocumentKey(documentId);
            var userId = GetUserIdFromUserInfo(userInfo);

            if (userId == null)
            {
                _logger.LogWarning("User attempted to join document {DocumentId} with null userId", documentId);
                return false;
            }

            _activeUsers.AddOrUpdate(
                documentKey,
                // If the key doesn't exist, create a new list with the user
                new List<object> { userInfo },
                // If the key exists, add the user to the existing list
                (key, list) =>
                {
                    // Only add if not already in the list
                    if (!list.Any(u =>
                        GetUserIdFromUserInfo(u) != null &&
                        GetUserIdFromUserInfo(u).Equals(userId)))
                    {
                        list.Add(userInfo);
                    }
                    return list;
                }
            );

            return true;
        }

        public bool RemoveUser(int documentId, string connectionId)
        {
            string documentKey = GetDocumentKey(documentId);
            bool isLastUser = false;

            if (_activeUsers.TryGetValue(documentKey, out var users))
            {
                // Find the user by the given identifier
                var userToRemove = users.FirstOrDefault(u =>
                    GetUserIdFromUserInfo(u)?.ToString() == connectionId);

                if (userToRemove != null)
                {
                    users.Remove(userToRemove);

                    // If the document has no active users, remove it from the dictionary
                    if (users.Count == 0)
                    {
                        _activeUsers.TryRemove(documentKey, out _);
                        isLastUser = true;
                    }

                    return true;
                }
            }

            return false;
        }

        public List<object> GetOtherUsers(int documentId, object currentUser)
        {
            string documentKey = GetDocumentKey(documentId);
            var activeUsers = _activeUsers.GetValueOrDefault(documentKey, new List<object>());
            var currentUserId = GetUserIdFromUserInfo(currentUser);

            // Filter out the current user from the list
            return activeUsers
                .Where(u =>
                    GetUserIdFromUserInfo(u) != null &&
                    currentUserId != null &&
                    !GetUserIdFromUserInfo(u).Equals(currentUserId))
                .ToList();
        }

        public bool IsLastUser(int documentId)
        {
            string documentKey = GetDocumentKey(documentId);
            return !_activeUsers.ContainsKey(documentKey) ||
                   _activeUsers[documentKey].Count == 0;
        }

        public static object GetUserIdFromUserInfo(object userInfo)
        {
            try
            {
                if (userInfo == null) return null;

                var user = JsonSerializer.Deserialize<Dictionary<string, object>>(userInfo.ToString());
                return user["id"];
            }
            catch
            {
                // If we can't access the id, return null
                return null;
            }
        }

        private static string GetDocumentKey(int documentId) => $"document_{documentId}";
    }
}