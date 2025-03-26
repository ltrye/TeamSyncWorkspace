using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Services;
using TeamSyncWorkspace.Services.ColabDocServices;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class DocumentUpdateHandler
    {
        private readonly TempDocumentManager _documentManager;
        private readonly ILogger<DocumentUpdateHandler> _logger;

        public DocumentUpdateHandler(
            TempDocumentManager documentManager,
            ILogger<DocumentUpdateHandler> logger)
        {
            _documentManager = documentManager;
            _logger = logger;
        }

        public async Task HandleUpdateDocument(
            IHubCallerClients clients,
            int documentId,
            int userId,
            string content)
        {
            string documentKey = $"document_{documentId}";

            try
            {
                // Update the temporary document in cache
                bool inCache = await _documentManager.UpdateDocumentAsync(documentId, content);

                // Broadcast changes to all other clients
                await clients.OthersInGroup(documentKey).SendAsync("ReceiveDocumentUpdate", userId, content);

                if (inCache)
                {
                    _logger.LogInformation("Document {DocumentId} updated by user {UserId} in temporary cache", documentId, userId);
                }
                else
                {
                    _logger.LogWarning("Document {DocumentId} updated directly in database by user {UserId}", documentId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating document {DocumentId}", documentId);
                throw;
            }
        }
    }
}