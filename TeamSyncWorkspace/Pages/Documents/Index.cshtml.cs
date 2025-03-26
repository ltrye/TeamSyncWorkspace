using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Documents
{
    [Authorize]
    public class IndexModel : PageModel
    {
        #region Services and Constructor

        private readonly DocumentService _documentService;
        private readonly TeamService _teamService;
        private readonly TeamRoleService _teamRoleService;
        private readonly WorkspaceService _workspaceService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            DocumentService documentService,
            TeamService teamService,
            TeamRoleService teamRoleService,
            WorkspaceService workspaceService,
            UserManager<ApplicationUser> userManager,
            ILogger<IndexModel> logger)
        {
            _documentService = documentService;
            _teamService = teamService;
            _teamRoleService = teamRoleService;
            _workspaceService = workspaceService;
            _userManager = userManager;
            _logger = logger;
        }

        #endregion

        #region Properties

        public List<CollabDoc> Documents { get; set; } = new List<CollabDoc>();
        public string WorkspaceId { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; }
        public int CurrentUserId { get; set; }
        public bool CanCreateDocuments { get; set; }
        public bool CanDeleteDocuments { get; set; }
        public bool IsTeamAdmin { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        #endregion

        #region OnGetAsync

        public async Task<IActionResult> OnGetAsync(string workspaceId)
        {
            if (string.IsNullOrEmpty(workspaceId))
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            CurrentUserId = user.Id;
            WorkspaceId = workspaceId;

            // Get workspace information
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(workspaceId);
            if (workspace == null)
            {
                return NotFound($"Workspace with ID '{workspaceId}' not found.");
            }

            TeamId = workspace.TeamId;
            var team = await _teamService.GetTeamByIdAsync(workspace.TeamId);
            TeamName = team?.TeamName ?? "Team";

            // Check if user is a member of the team
            bool isMember = await _teamService.IsUserTeamMemberAsync(workspace.TeamId, user.Id);
            if (!isMember)
            {
                return Forbid();
            }

            // Get documents for this workspace
            Documents = await _documentService.GetDocumentsByWorkspaceIdAsync(workspaceId);

            // Get permission information
            IsTeamAdmin = await _teamService.IsUserTeamAdminAsync(workspace.TeamId, user.Id);
            CanCreateDocuments = IsTeamAdmin || await _teamRoleService.UserCanPerformActionAsync(workspace.TeamId, user.Id, ActivityType.CreateDocument);
            CanDeleteDocuments = IsTeamAdmin || await _teamRoleService.UserCanPerformActionAsync(workspace.TeamId, user.Id, ActivityType.DeleteDocument);

            return Page();
        }

        #endregion

        #region OnPostCreateDocumentAsync

        public async Task<IActionResult> OnPostCreateDocumentAsync(string workspaceId, string title, string description = "")
        {
            if (string.IsNullOrEmpty(workspaceId) || string.IsNullOrEmpty(title))
            {
                return NotFound("Workspace ID and document title are required.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get workspace information
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(workspaceId);
            if (workspace == null)
            {
                return NotFound($"Workspace with ID '{workspaceId}' not found.");
            }

            // Check permissions
            bool canCreate = await _teamRoleService.UserCanPerformActionAsync(workspace.TeamId, user.Id, ActivityType.CreateDocument);

            if (!canCreate)
            {
                StatusMessage = "Error: You don't have permission to create documents.";
                return RedirectToPage(new { workspaceId });
            }

            // Create the document
            try
            {
                await _documentService.CreateDocumentAsync(workspaceId, user.Id, title, description);
                StatusMessage = "Document created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document");
                StatusMessage = $"Error: {ex.Message}";
            }

            return RedirectToPage(new { workspaceId });
        }

        #endregion

        #region OnPostDeleteDocumentAsync

        public async Task<IActionResult> OnPostDeleteDocumentAsync(int documentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get document information
            var document = await _documentService.GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                return NotFound($"Document with ID '{documentId}' not found.");
            }

            // Get workspace and team information
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(document.WorkspaceId);
            if (workspace == null)
            {
                return NotFound($"Workspace for document '{documentId}' not found.");
            }

            // Check permissions
            bool isAdmin = await _teamService.IsUserTeamAdminAsync(workspace.TeamId, user.Id);
            bool isOwner = document.CreatedByUserId == user.Id;
            bool canDelete = isAdmin || (isOwner && await _teamRoleService.UserCanPerformActionAsync(workspace.TeamId, user.Id, ActivityType.DeleteDocument));

            if (!canDelete)
            {
                StatusMessage = "Error: You don't have permission to delete this document.";
                return RedirectToPage(new { workspaceId = document.WorkspaceId });
            }

            // Delete the document
            try
            {
                await _documentService.DeleteDocumentAsync(documentId);
                StatusMessage = "Document deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document");
                StatusMessage = $"Error: {ex.Message}";
            }

            return RedirectToPage(new { workspaceId = document.WorkspaceId });
        }

        #endregion
    }
}