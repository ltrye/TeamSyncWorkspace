using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models.Documents;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class ChatHandler
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ChatHandler> _logger;

        public ChatHandler(AppDbContext context, ILogger<ChatHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task HandleSaveChatMessage(int documentId, int userId, string message)
        {
            try
            {
                var chatMessage = new ChatMessage
                {
                    DocumentId = documentId,
                    UserId = userId,
                    Content = message,
                    SentAt = DateTime.UtcNow
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Chat message saved for document {DocumentId} from user {UserId}", documentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat message for document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task<List<object>> GetChatHistory(int documentId)
        {
            try
            {
                var messages = await _context.ChatMessages
                    .Where(m => m.DocumentId == documentId)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.UserId,
                        UserName = m.User.UserName,
                        m.Content,
                        m.SentAt
                    })
                    .ToListAsync();

                return messages.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history for document {DocumentId}", documentId);
                throw;
            }
        }
    }
}