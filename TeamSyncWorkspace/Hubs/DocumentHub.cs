using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Hubs
{
    [Authorize]
    public class DocumentHub : Hub
    {
        private readonly DocumentService _documentService;
        private readonly ILogger<DocumentHub> _logger;
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<string, List<object>> _activeUsers = new ConcurrentDictionary<string, List<object>>();
        private static readonly ConcurrentDictionary<string, Timer> _persistenceTimers = new ConcurrentDictionary<string, Timer>();
        private static readonly TimeSpan _persistInterval = TimeSpan.FromSeconds(2);

        public DocumentHub(
            DocumentService documentService,
            ILogger<DocumentHub> logger,
            IMemoryCache cache)
        {
            _documentService = documentService;
            _logger = logger;
            _cache = cache;
        }

        public async Task JoinDocument(int documentId, object userInfo)
        {
            string documentKey = $"document_{documentId}";
            string cacheKey = $"temp_doc_{documentId}";

            try
            {
                // Add user to SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, documentKey);

                // Add user to active users list
                _activeUsers.AddOrUpdate(
                    documentKey,
                    // If the key doesn't exist, create a new list with the user
                    new List<object> { userInfo },
                    // If the key exists, add the user to the existing list
                    (key, list) =>
                    {
                        // Only add if not already in the list (based on id property)
                        var userId = GetUserIdFromUserInfo(userInfo);
                        _logger.LogInformation("User ID: {UserId}", userId);
                        if (userId == null)
                        {
                            _logger.LogWarning("User attempted to join document {DocumentId} with null userId", documentId);
                            return list;
                        }
                        if (userId != null && !list.Any(u =>
                            GetUserIdFromUserInfo(u) != null &&
                            GetUserIdFromUserInfo(u).Equals(userId)))
                        {
                            list.Add(userInfo);
                        }
                        return list;
                    }
                );

                // Initialize temporary document in cache if it doesn't exist
                if (!_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
                {
                    // This is the first user to join this document
                    // Retrieve the document from database
                    var document = await _documentService.GetDocumentByIdAsync(documentId);
                    if (document != null)
                    {
                        tempDoc = new TempDocument
                        {
                            DocumentId = documentId,
                            Content = document.Content,
                            LastSaved = DateTime.UtcNow,
                            IsDirty = false
                        };

                        // Add to cache
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetPriority(CacheItemPriority.High);
                        _cache.Set(cacheKey, tempDoc, cacheOptions);

                        // Create a timer to periodically persist changes
                        Timer timer = new(async (state) => await PersistDocument(state), documentId, _persistInterval, _persistInterval);
                        _persistenceTimers.TryAdd(documentKey, timer);

                        _logger.LogInformation("Created temporary document for {DocumentId}", documentId);
                    }
                }

                // Notify other users that this user joined
                await Clients.OthersInGroup(documentKey).SendAsync("UserJoined", userInfo);

                // Send the current list of active users to the joining user
                var activeUsers = _activeUsers.GetValueOrDefault(documentKey, new List<object>());

                // Filter out the current user from the list
                var otherUsers = activeUsers
                    .Where(u =>
                        GetUserIdFromUserInfo(u) != null &&
                        GetUserIdFromUserInfo(userInfo) != null &&
                        !GetUserIdFromUserInfo(u).Equals(GetUserIdFromUserInfo(userInfo)))
                    .ToList();

                await Clients.Caller.SendAsync("ActiveUsers", otherUsers);

                // Send the latest document content to the joining user
                if (tempDoc != null)
                {
                    await Clients.Caller.SendAsync("ReceiveDocumentUpdate", 0, tempDoc.Content);
                }

                _logger.LogInformation("User joined document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while user was joining document {DocumentId}", documentId);
            }
        }

        public async Task UpdateDocument(int documentId, int userId, string content)
        {
            try
            {
                string cacheKey = $"temp_doc_{documentId}";
                string documentKey = $"document_{documentId}";

                // Update the temporary document in cache
                if (_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
                {
                    // Update the content
                    tempDoc.Content = content;
                    tempDoc.IsDirty = true;
                    tempDoc.LastModified = DateTime.UtcNow;

                    // Update the cache entry
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetPriority(CacheItemPriority.High);
                    _cache.Set(cacheKey, tempDoc, cacheOptions);

                    // Broadcast changes to all other clients
                    await Clients.OthersInGroup(documentKey).SendAsync("ReceiveDocumentUpdate", userId, content);
                    _logger.LogInformation("Document {DocumentId} updated by user {UserId} in temporary cache", documentId, userId);
                }
                else
                {
                    // Fallback to direct database update and broadcast if cache entry not found
                    await _documentService.UpdateDocumentContentAsync(documentId, content, userId);
                    await Clients.OthersInGroup(documentKey).SendAsync("ReceiveDocumentUpdate", userId, content);
                    _logger.LogWarning("Temp document not found in cache for {DocumentId}, updating directly in database", documentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating document {DocumentId}", documentId);
            }
        }

        public async Task LeaveDocument(int documentId)
        {
            try
            {
                string documentKey = $"document_{documentId}";

                // Remove user from SignalR group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentKey);

                // Remove from active users list if present
                bool isLastUser = false;
                if (_activeUsers.TryGetValue(documentKey, out var users))
                {
                    // Find the user by Context.UserIdentifier
                    var userToRemove = users.FirstOrDefault(u =>
                        GetUserIdFromUserInfo(u)?.ToString() == Context.UserIdentifier);

                    if (userToRemove != null)
                    {
                        users.Remove(userToRemove);

                        // If the document has no active users, remove it from the dictionary
                        if (users.Count == 0)
                        {
                            _activeUsers.TryRemove(documentKey, out _);
                            isLastUser = true;
                        }
                    }
                }

                // If this was the last user, save the document and stop the timer
                if (isLastUser)
                {
                    await PersistDocumentFinal(documentId);
                }

                // Notify others that user left
                await Clients.OthersInGroup(documentKey).SendAsync("UserLeft", Context.UserIdentifier);

                _logger.LogInformation("User left document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while user was leaving document {DocumentId}", documentId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                // When a user disconnects, we need to remove them from all documents they were editing
                if (Context.UserIdentifier == null)
                {
                    _logger.LogWarning("User disconnected with null UserIdentifier");
                    await base.OnDisconnectedAsync(exception);
                    return;
                }

                foreach (var kvp in _activeUsers)
                {
                    string documentKey = kvp.Key;
                    var usersInDocument = kvp.Value;
                    if (usersInDocument == null) continue;

                    // Try to find and remove the disconnected user
                    var userToRemove = usersInDocument.FirstOrDefault(u =>
                        u != null &&
                        GetUserIdFromUserInfo(u)?.ToString() == Context.UserIdentifier);

                    if (userToRemove != null)
                    {
                        // Remove user from the active users list
                        bool isLastUser = false;
                        if (_activeUsers.TryGetValue(documentKey, out var users))
                        {
                            users.Remove(userToRemove);

                            // If the document has no active users, remove it from the dictionary
                            if (users.Count == 0)
                            {
                                _activeUsers.TryRemove(documentKey, out _);
                                isLastUser = true;
                            }
                        }

                        // If this was the last user, save the document and stop the timer
                        if (isLastUser)
                        {
                            int documentId = int.Parse(documentKey.Replace("document_", ""));
                            await PersistDocumentFinal(documentId);
                        }

                        // Notify others that user left
                        await Clients.OthersInGroup(documentKey).SendAsync("UserLeft", Context.UserIdentifier);

                        _logger.LogInformation("User disconnected from document {DocumentId}", documentKey.Replace("document_", ""));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Persist document changes to database (called periodically by timer)
        private async Task PersistDocument(object state)
        {
            int documentId = (int)state;
            string cacheKey = $"temp_doc_{documentId}";

            try
            {
                if (_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
                {
                    // Only save if the document has changes
                    if (tempDoc.IsDirty)
                    {
                        await _documentService.UpdateDocumentContentAsync(documentId, tempDoc.Content, 0);

                        // Mark as no longer dirty after saving
                        tempDoc.IsDirty = false;
                        tempDoc.LastSaved = DateTime.UtcNow;

                        // Update the cache
                        _cache.Set(cacheKey, tempDoc);

                        _logger.LogInformation("Persisted temporary document {DocumentId} to database", documentId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting document {DocumentId}", documentId);
            }
        }

        // Final persistence when all users leave the document
        private async Task PersistDocumentFinal(int documentId)
        {
            string documentKey = $"document_{documentId}";
            string cacheKey = $"temp_doc_{documentId}";

            try
            {
                // Stop the persistence timer
                if (_persistenceTimers.TryRemove(documentKey, out Timer timer))
                {
                    timer.Dispose();
                }

                // Get the temporary document from cache
                if (_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
                {
                    // Save the final state to the database if it has changes
                    if (tempDoc.IsDirty)
                    {
                        await _documentService.UpdateDocumentContentAsync(documentId, tempDoc.Content, 0);
                        _logger.LogInformation("Final persistence of document {DocumentId} completed", documentId);
                    }

                    // Remove from cache
                    _cache.Remove(cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during final persistence of document {DocumentId}", documentId);
            }
        }

        // Other methods (SendOperation, AddComment, etc.) remain the same...

        // Helper method to extract user ID from the userInfo object
        private static object GetUserIdFromUserInfo(object userInfo)
        {
            var user = JsonSerializer.Deserialize<Dictionary<string, object>>(userInfo.ToString());
            Console.WriteLine(user["id"]);
            try
            {
                if (userInfo == null) return null;

                return user["id"];
            }
            catch
            {
                // If we can't access the id, return null
                return null;
            }
        }

        public async Task UpdateDocumentContent(int documentId, string content, object user)
        {
            // Broadcast the updated content to all other clients in the group
            await Clients.OthersInGroup(documentId.ToString()).SendAsync("ContentUpdated", content, user);
        }

        public async Task SendOperation(int documentId, int userId, string operationType, string operationData)
        {
            try
            {
                await Clients.OthersInGroup($"document_{documentId}").SendAsync("ReceiveOperation", userId, operationType, operationData);
                _logger.LogInformation("Operation {OperationType} sent for document {DocumentId} by user {UserId}", operationType, documentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending operation for document {DocumentId}", documentId);
            }
        }

        public async Task AddComment(string documentId, int userId, string userName, string content, string rangeData)
        {
            try
            {
                await Clients.OthersInGroup($"document_{documentId}").SendAsync("ReceiveComment", userId, userName, content, rangeData);
                _logger.LogInformation("Comment added to document {DocumentId} by user {UserId}", documentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding comment to document {DocumentId}", documentId);
            }
        }

        public async Task ResolveComment(string documentId, int commentId, int userId)
        {
            try
            {
                await Clients.OthersInGroup($"document_{documentId}").SendAsync("CommentResolved", commentId, userId);
                _logger.LogInformation("Comment {CommentId} resolved in document {DocumentId} by user {UserId}", commentId, documentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while resolving comment in document {DocumentId}", documentId);
            }
        }

        public async Task SendCursorPosition(int documentId, int userId, object userInfo, string cursorData)
        {
            try
            {
                string documentKey = $"document_{documentId}";
                await Clients.OthersInGroup(documentKey).SendAsync("CursorPosition", userId, userInfo, cursorData);

                // Optional: Log cursor position updates (but might be too verbose)
                // _logger.LogDebug("Cursor position update in document {DocumentId} from user {UserId}", documentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending cursor position for document {DocumentId}", documentId);
            }
        }
    }

    // Model for temporary document in cache
    public class TempDocument
    {
        public int DocumentId { get; set; }
        public string Content { get; set; }
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime LastSaved { get; set; }
        public bool IsDirty { get; set; }
    }
}