using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Hubs.Handlers;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Hubs
{
    [Authorize]
    public class DocumentHub : Hub
    {
        private readonly DocumentJoinHandler _joinHandler;
        private readonly DocumentUpdateHandler _updateHandler;
        private readonly DocumentLeaveHandler _leaveHandler;
        private readonly CursorPositionHandler _cursorHandler;
        private readonly CommentHandler _commentHandler;
        private readonly ChatHandler _chatHandler;
        private readonly ILogger<DocumentHub> _logger;

        public DocumentHub(
            DocumentJoinHandler joinHandler,
            DocumentUpdateHandler updateHandler,
            DocumentLeaveHandler leaveHandler,
            CursorPositionHandler cursorHandler,
            CommentHandler commentHandler,
            ChatHandler chatHandler,
            ILogger<DocumentHub> logger)
        {
            _joinHandler = joinHandler;
            _updateHandler = updateHandler;
            _leaveHandler = leaveHandler;
            _cursorHandler = cursorHandler;
            _commentHandler = commentHandler;
            _logger = logger;
            _chatHandler = chatHandler;
        }

        public async Task JoinDocument(int documentId, object userInfo)
        {
            // Store documentId in connection context for later use
            Context.Items["documentId"] = documentId;

            await _joinHandler.HandleJoinDocument(Clients, Groups, Context.ConnectionId, documentId, userInfo);
        }

        public async Task UpdateDocument(int documentId, int userId, string content, object delta)
        {
            await _updateHandler.HandleUpdateDocument(Clients, documentId, userId, content, delta);
        }

        public async Task LeaveDocument(int documentId)
        {
            await _leaveHandler.HandleLeaveDocument(Clients, Groups, Context.ConnectionId, documentId, Context.UserIdentifier);
        }

        public async Task SendCursorPosition(int documentId, int userId, object userInfo, string cursorData)
        {
            await _cursorHandler.HandleCursorPosition(Clients, documentId, userId, userInfo, cursorData);
        }

        public async Task AddComment(string documentId, int userId, string userName, string content, string rangeData)
        {
            await _commentHandler.HandleAddComment(Clients, documentId, userId, userName, content, rangeData);
        }

        public async Task ResolveComment(string documentId, int commentId, int userId)
        {
            await _commentHandler.HandleResolveComment(Clients, documentId, commentId, userId);
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

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                // When a user disconnects, we need to handle this in the leave handler
                // Get documentId from connection context or track it when user joins
                int? documentId = null;

                // Try to get the documentId from connection context items
                if (Context.Items.ContainsKey("documentId"))
                {
                    documentId = (int)Context.Items["documentId"];
                }

                // Only call leave handler if we have a valid document ID
                if (documentId.HasValue)
                {
                    await _leaveHandler.HandleLeaveDocument(Clients, Groups, Context.ConnectionId, documentId.Value, Context.UserIdentifier);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
                await base.OnDisconnectedAsync(exception);
            }
        }
        // Add to DocumentHub.cs
        public async Task SendChatMessage(int documentId, int userId, object userInfo, string message)
        {
            string documentKey = $"document_{documentId}";
            try
            {
                // Broadcast to all clients in the group
                await Clients.Group(documentKey).SendAsync("ReceiveChatMessage", userId, userInfo, message, DateTime.UtcNow);

                // Persist message to database (using a handler)
                await _chatHandler.HandleSaveChatMessage(documentId, userId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message for document {DocumentId}", documentId);
            }
        }

        public async Task LoadChatHistory(int documentId)
        {
            try
            {
                var messages = await _chatHandler.GetChatHistory(documentId);
                await Clients.Caller.SendAsync("ChatHistory", messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat history for document {DocumentId}", documentId);
            }
        }

    }
}