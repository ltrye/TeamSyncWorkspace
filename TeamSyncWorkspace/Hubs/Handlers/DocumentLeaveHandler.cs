using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Services;
using TeamSyncWorkspace.Services.ColabDocServices;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class DocumentLeaveHandler
    {
        private readonly TempDocumentManager _documentManager;
        private readonly DocumentCollaborationService _collaborationService;
        private readonly ILogger<DocumentLeaveHandler> _logger;

        public DocumentLeaveHandler(
            TempDocumentManager documentManager,
            DocumentCollaborationService collaborationService,
            ILogger<DocumentLeaveHandler> logger)
        {
            _documentManager = documentManager;
            _collaborationService = collaborationService;
            _logger = logger;
        }

        public async Task HandleLeaveDocument(
            IHubCallerClients clients,
            IGroupManager groups,
            string connectionId,
            int documentId,
            string userId)
        {
            string documentKey = $"document_{documentId}";

            Console.WriteLine("DocumentLeaveHandler: " + documentKey);
            Console.WriteLine("UserId: " + userId);
            try
            {
                // Remove user from SignalR group
                await groups.RemoveFromGroupAsync(connectionId, documentKey);

                // Remove from active users list
                _collaborationService.RemoveUser(documentId, userId);

                // If this was the last user, save the document and clean up
                if (_collaborationService.IsLastUser(documentId))
                {
                    await _documentManager.FinalizeDocumentAsync(documentId);
                }

                // Notify others that user left
                await clients.OthersInGroup(documentKey).SendAsync("UserLeft", userId);

                _logger.LogInformation("User left document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while user was leaving document {DocumentId}", documentId);
                throw;
            }
        }
    }
}