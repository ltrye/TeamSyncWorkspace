using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Services;
using TeamSyncWorkspace.Services.ColabDocServices;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class DocumentJoinHandler
    {
        private readonly TempDocumentManager _documentManager;
        private readonly DocumentCollaborationService _collaborationService;
        private readonly ILogger<DocumentJoinHandler> _logger;

        public DocumentJoinHandler(
            TempDocumentManager documentManager,
            DocumentCollaborationService collaborationService,
            ILogger<DocumentJoinHandler> logger)
        {
            _documentManager = documentManager;
            _collaborationService = collaborationService;
            _logger = logger;
        }

        public async Task HandleJoinDocument(
            IHubCallerClients clients,
            IGroupManager groups,
            string connectionId,
            int documentId,
            object userInfo)
        {
            string documentKey = $"document_{documentId}";

            try
            {
                // Add user to SignalR group
                await groups.AddToGroupAsync(connectionId, documentKey);

                // Add user to active users list
                _collaborationService.AddUser(documentId, userInfo);

                // Initialize temporary document
                var tempDoc = await _documentManager.InitializeDocumentAsync(documentId);

                // Notify other users that this user joined
                await clients.OthersInGroup(documentKey).SendAsync("UserJoined", userInfo);

                // Send other active users to the joining user
                var otherUsers = _collaborationService.GetOtherUsers(documentId, userInfo);
                await clients.Caller.SendAsync("ActiveUsers", otherUsers);

                // Send the latest document content to the joining user
                if (tempDoc != null)
                {
                    await clients.Caller.SendAsync("ReceiveDocumentUpdate", 0, tempDoc.Content);
                }

                _logger.LogInformation("User joined document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while user was joining document {DocumentId}", documentId);
                throw;
            }
        }
    }
}