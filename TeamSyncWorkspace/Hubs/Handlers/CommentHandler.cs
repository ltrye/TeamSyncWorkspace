using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class CommentHandler
    {
        private readonly ILogger<CommentHandler> _logger;

        public CommentHandler(ILogger<CommentHandler> logger)
        {
            _logger = logger;
        }

        public async Task HandleAddComment(
            IHubCallerClients clients,
            string documentId,
            int userId,
            string userName,
            string content,
            string rangeData)
        {
            try
            {
                await clients.OthersInGroup($"document_{documentId}").SendAsync(
                    "ReceiveComment", userId, userName, content, rangeData);

                _logger.LogInformation("Comment added to document {DocumentId} by user {UserId}", documentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding comment to document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task HandleResolveComment(
            IHubCallerClients clients,
            string documentId,
            int commentId,
            int userId)
        {
            try
            {
                await clients.OthersInGroup($"document_{documentId}").SendAsync(
                    "CommentResolved", commentId, userId);

                _logger.LogInformation("Comment {CommentId} resolved in document {DocumentId} by user {UserId}",
                    commentId, documentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while resolving comment in document {DocumentId}", documentId);
                throw;
            }
        }
    }
}