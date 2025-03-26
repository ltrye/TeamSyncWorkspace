using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class CursorPositionHandler
    {
        private readonly ILogger<CursorPositionHandler> _logger;

        public CursorPositionHandler(ILogger<CursorPositionHandler> logger)
        {
            _logger = logger;
        }

        public async Task HandleCursorPosition(
            IHubCallerClients clients,
            int documentId,
            int userId,
            object userInfo,
            string cursorData)
        {
            try
            {
                string documentKey = $"document_{documentId}";
                await clients.OthersInGroup(documentKey).SendAsync("CursorPosition", userId, userInfo, cursorData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending cursor position for document {DocumentId}", documentId);
                throw;
            }
        }
    }
}