using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Models.Documents;

namespace TeamSyncWorkspace.Services
{
    public class DocumentService
    {
        private readonly AppDbContext _context;
        private readonly TeamService _teamService;
        private readonly TeamRoleService _teamRoleService;
        private readonly WorkspaceService _workspaceService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            AppDbContext context,
            TeamService teamService,
            TeamRoleService teamRoleService,
            WorkspaceService workspaceService,
            IWebHostEnvironment webHostEnvironment,
            ILogger<DocumentService> logger)
        {
            _context = context;
            _teamService = teamService;
            _teamRoleService = teamRoleService;
            _workspaceService = workspaceService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        public async Task<List<CollabDoc>> GetDocumentsByWorkspaceIdAsync(string workspaceId)
        {
            return await _context.CollabDocs
                .Include(d => d.CreatedByUser)
                .Where(d => d.WorkspaceId == workspaceId)
                .OrderByDescending(d => d.LastModifiedDate)
                .ToListAsync();
        }

        public async Task<CollabDoc> GetDocumentByIdAsync(int id)
        {
            return await _context.CollabDocs
                .Include(d => d.CreatedByUser)
                .FirstOrDefaultAsync(d => d.DocId == id);
        }

        public async Task<CollabDoc> CreateDocumentAsync(string workspaceId, int userId, string title, string description = "")
        {
            var document = new CollabDoc
            {
                WorkspaceId = workspaceId,
                Title = title,
                Content = "<h1>" + title + "</h1><p>" + description + "</p>",
                CreatedByUserId = userId,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _context.CollabDocs.Add(document);
            await _context.SaveChangesAsync();

            return document;
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            // Find document
            var document = await _context.CollabDocs.FindAsync(id);
            if (document == null)
            {
                return false;
            }

            // Remove document actions first to maintain referential integrity
            var actions = await _context.DocActions
                .Where(a => a.DocId == id)
                .ToListAsync();

            if (actions.Any())
            {
                _context.DocActions.RemoveRange(actions);
            }

            // Remove document
            _context.CollabDocs.Remove(document);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task UpdateDocumentContentAsync(int id, string content, int userId)
        {
            var document = await _context.CollabDocs.FindAsync(id);
            if (document != null)
            {
                document.Content = content;
                document.LastModifiedDate = DateTime.UtcNow;

                // // Create a new document action for tracking changes
                // var action = new DocAction
                // {
                //     DocId = id,
                //     UserId = userId,
                //     ActionType = "Update",
                //     ActionData = "{ \"type\": \"content\" }",
                //     Timestamp = DateTime.UtcNow
                // };

                // _context.DocActions.Add(action);
                _context.CollabDocs.Update(document);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDocumentTitleAsync(int id, string title, int userId)
        {
            var document = await _context.CollabDocs.FindAsync(id);
            if (document != null)
            {
                document.Title = title;
                document.LastModifiedDate = DateTime.UtcNow;

                // Create a new document action for tracking title changes
                var action = new DocAction
                {
                    DocId = id,
                    UserId = userId,
                    ActionType = "Update",
                    ActionData = "{ \"type\": \"title\" }",
                    Timestamp = DateTime.UtcNow
                };

                _context.DocActions.Add(action);
                _context.CollabDocs.Update(document);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> UserCanEditDocumentAsync(int docId, int userId)
        {
            var document = await _context.CollabDocs.FindAsync(docId);
            if (document == null)
            {
                return false;
            }

            // Get workspace and team information
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(document.WorkspaceId);
            if (workspace == null)
            {
                return false;
            }

            // Document owners can always edit their documents
            if (document.CreatedByUserId == userId)
            {
                return true;
            }

            // Team admins can edit any document
            // bool isTeamAdmin = await _teamService.IsUserTeamAdminAsync(workspace.TeamId, userId);
            // if (isTeamAdmin)
            // {
            //     return true;
            // }

            // Check team role permissions
            return await _teamRoleService.UserCanPerformActionAsync(workspace.TeamId, userId, ActivityType.EditDocument);
        }

        public async Task<bool> UserCanViewDocumentAsync(int docId, int userId)
        {
            var document = await _context.CollabDocs.FindAsync(docId);
            if (document == null)
            {
                return false;
            }

            // Get workspace and team information
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(document.WorkspaceId);
            if (workspace == null)
            {
                return false;
            }

            // Check if user is a member of the team
            return await _teamService.IsUserTeamMemberAsync(workspace.TeamId, userId);
        }

        public async Task<List<DocAction>> GetRecentActionsAsync(int docId, int limit = 20)
        {
            return await _context.DocActions
                .Include(a => a.User)
                .Where(a => a.DocId == docId)
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .ToListAsync();
        }

        public async Task TrackDocumentOperationAsync(int docId, int userId, string operationType, string operationData)
        {
            var action = new DocAction
            {
                DocId = docId,
                UserId = userId,
                ActionType = operationType,
                ActionData = operationData,
                Timestamp = DateTime.UtcNow
            };

            _context.DocActions.Add(action);
            await _context.SaveChangesAsync();
        }

        public async Task<List<DocAction>> GetDocumentOperationsAsync(int docId, DateTime sinceTimestamp)
        {
            return await _context.DocActions
                .Where(op => op.DocId == docId && op.Timestamp > sinceTimestamp)
                .OrderBy(op => op.Timestamp)
                .ToListAsync();
        }

        public async Task<byte[]> ExportDocumentAsPdfAsync(int docId)
        {
            var document = await _context.CollabDocs.FindAsync(docId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID '{docId}' not found.");
            }

            // In a real implementation, you would use a proper PDF library like iText, DinkToPdf, etc.
            using (var memoryStream = new MemoryStream())
            using (var streamWriter = new StreamWriter(memoryStream))
            {
                streamWriter.Write(document.Content);
                streamWriter.Flush();
                return memoryStream.ToArray();
            }
        }

        public async Task<byte[]> ExportDocumentAsDocxAsync(int docId)
        {
            var document = await _context.CollabDocs.FindAsync(docId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID '{docId}' not found.");
            }

            // In a real implementation, you would use a proper DOCX library like OpenXML
            using (var memoryStream = new MemoryStream())
            using (var streamWriter = new StreamWriter(memoryStream))
            {
                streamWriter.Write(document.Content);
                streamWriter.Flush();
                return memoryStream.ToArray();
            }
        }

        public async Task<List<ApplicationUser>> GetActiveCollaboratorsAsync(int docId)
        {
            // Consider users active if they have performed an action in the last 15 minutes
            var recentTimespan = DateTime.UtcNow.AddMinutes(-15);

            var activeUserIds = await _context.DocActions
                .Where(a => a.DocId == docId && a.Timestamp > recentTimespan && a.UserId != null)
                .Select(a => a.UserId.Value)
                .Distinct()
                .ToListAsync();

            var activeUsers = new List<ApplicationUser>();

            foreach (var userId in activeUserIds)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    activeUsers.Add(user);
                }
            }

            return activeUsers;
        }


        /// <summary>
        /// Adds a comment to a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <param name="userId">The user ID of the commenter</param>
        /// <param name="content">The comment text</param>
        /// <param name="rangeStart">JSON string representing the start point of text selection</param>
        /// <param name="rangeEnd">JSON string representing the end point of text selection</param>
        /// <returns>The created comment</returns>
        public async Task<DocumentComment> AddDocumentCommentAsync(string documentId, int userId, string content, string rangeStart = null, string rangeEnd = null)
        {
            // Convert string documentId to int for our CollabDoc model
            if (!int.TryParse(documentId, out int docId))
            {
                throw new ArgumentException($"Invalid document ID format: {documentId}");
            }

            // Get document to ensure it exists
            var document = await _context.CollabDocs.FindAsync(docId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID '{documentId}' not found.");
            }

            // Check if user is allowed to comment (has view access)
            bool canView = await UserCanViewDocumentAsync(docId, userId);
            if (!canView)
            {
                throw new UnauthorizedAccessException("User doesn't have permission to comment on this document.");
            }

            // Create the comment
            var comment = new DocumentComment
            {
                DocumentId = int.Parse(documentId),
                UserId = userId,
                Content = content,
                RangeStart = rangeStart,
                RangeEnd = rangeEnd,
                CreatedDate = DateTime.UtcNow,
                IsResolved = false
            };

            _context.DocumentComments.Add(comment);

            // Add an action entry for this comment
            var action = new DocAction
            {
                DocId = docId,
                UserId = userId,
                ActionType = "Comment",
                ActionData = $"{{ \"commentId\": {comment.CommentId}, \"content\": \"{content.Substring(0, Math.Min(content.Length, 50))}...\" }}",
                Timestamp = DateTime.UtcNow
            };

            _context.DocActions.Add(action);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Comment added to document {DocumentId} by user {UserId}", documentId, userId);

            return comment;
        }

        /// <summary>
        /// Marks a comment as resolved
        /// </summary>
        /// <param name="commentId">The ID of the comment to resolve</param>
        /// <param name="userId">The ID of the user resolving the comment</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task ResolveDocumentCommentAsync(int commentId, int userId)
        {
            var comment = await _context.DocumentComments.FindAsync(commentId);
            if (comment == null)
            {
                throw new ArgumentException($"Comment with ID '{commentId}' not found.");
            }

            // Check if the comment is already resolved
            if (comment.IsResolved)
            {
                return; // Already resolved, nothing to do
            }

            // Check if document exists and convert string ID to int


            var document = await _context.CollabDocs.FindAsync(comment.DocumentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID '{comment.DocumentId}' not found.");
            }

            // Mark the comment as resolved
            comment.IsResolved = true;
            comment.ResolvedById = userId;
            comment.ResolvedDate = DateTime.UtcNow;

            _context.DocumentComments.Update(comment);

            // Add an action entry for resolving this comment
            var action = new DocAction
            {
                DocId = comment.DocumentId,
                UserId = userId,
                ActionType = "ResolveComment",
                ActionData = $"{{ \"commentId\": {commentId} }}",
                Timestamp = DateTime.UtcNow
            };

            _context.DocActions.Add(action);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Comment {CommentId} resolved by user {UserId}", commentId, userId);
        }

        /// <summary>
        /// Gets all comments for a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <param name="includeResolved">Whether to include resolved comments</param>
        /// <returns>List of document comments</returns>
        public async Task<List<DocumentComment>> GetDocumentCommentsAsync(string documentId, bool includeResolved = false)
        {
            // Convert string documentId to int for our CollabDoc model if needed
            if (!int.TryParse(documentId, out int _))
            {
                throw new ArgumentException($"Invalid document ID format: {documentId}");
            }

            var query = _context.DocumentComments
                .Include(c => c.User)
                .Include(c => c.ResolvedBy)
                .Where(c => c.DocumentId == int.Parse(documentId));

            if (!includeResolved)
            {
                query = query.Where(c => !c.IsResolved);
            }

            return await query
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a specific comment by ID
        /// </summary>
        /// <param name="commentId">The comment ID</param>
        /// <returns>The document comment or null if not found</returns>
        public async Task<DocumentComment> GetCommentByIdAsync(int commentId)
        {
            return await _context.DocumentComments
                .Include(c => c.User)
                .Include(c => c.ResolvedBy)
                .FirstOrDefaultAsync(c => c.CommentId == commentId);
        }


        /// <summary>
        /// Gets all permissions for a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>List of document permissions</returns>
        public async Task<List<DocumentPermission>> GetDocumentPermissionsAsync(string documentId)
        {
            if (!int.TryParse(documentId, out int docId))
            {
                _logger.LogWarning("Invalid document ID format: {DocumentId}", documentId);
                return new List<DocumentPermission>();
            }

            // Check if document exists
            var document = await _context.CollabDocs.FindAsync(docId);
            if (document == null)
            {
                _logger.LogWarning("Document not found: {DocumentId}", documentId);
                return new List<DocumentPermission>();
            }

            return await _context.DocumentPermissions
                .Include(p => p.User)
                .Where(p => p.DocumentId == int.Parse(documentId))
                .ToListAsync();
        }

        /// <summary>
        /// Sets permission for a user on a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <param name="userId">The user ID</param>
        /// <param name="canEdit">Whether the user can edit the document</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task SetDocumentPermissionAsync(string documentId, int userId, bool canEdit)
        {
            if (!int.TryParse(documentId, out int docId))
            {
                throw new ArgumentException($"Invalid document ID format: {documentId}");
            }

            // Check if document exists
            var document = await _context.CollabDocs.FindAsync(docId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID '{documentId}' not found.");
            }

            // Check if user exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException($"User with ID '{userId}' not found.");
            }

            var permission = await _context.DocumentPermissions
                .FirstOrDefaultAsync(p => p.DocumentId == int.Parse(documentId) && p.UserId == userId);

            if (permission == null)
            {
                // Create new permission
                permission = new DocumentPermission
                {
                    DocumentId = int.Parse(documentId),
                    UserId = userId,
                    CanEdit = canEdit
                };
                _context.DocumentPermissions.Add(permission);

                // Log the action
                var action = new DocAction
                {
                    DocId = docId,
                    UserId = userId,
                    ActionType = "Permission",
                    ActionData = $"{{ \"type\": \"add\", \"canEdit\": {canEdit.ToString().ToLower()} }}",
                    Timestamp = DateTime.UtcNow
                };
                _context.DocActions.Add(action);
            }
            else
            {
                // Update existing permission
                permission.CanEdit = canEdit;
                _context.DocumentPermissions.Update(permission);

                // Log the action
                var action = new DocAction
                {
                    DocId = docId,
                    UserId = userId,
                    ActionType = "Permission",
                    ActionData = $"{{ \"type\": \"update\", \"canEdit\": {canEdit.ToString().ToLower()} }}",
                    Timestamp = DateTime.UtcNow
                };
                _context.DocActions.Add(action);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Permission set for user {UserId} on document {DocumentId}: CanEdit={CanEdit}",
                userId, documentId, canEdit);
        }
    }




}