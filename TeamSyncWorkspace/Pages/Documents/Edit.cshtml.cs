using System;
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
    public class EditModel : PageModel
    {
        private readonly DocumentService _documentService;
        private readonly TeamService _teamService;
        private readonly WorkspaceService _workspaceService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            DocumentService documentService,
            TeamService teamService,
            UserManager<ApplicationUser> userManager,
            WorkspaceService workspaceService,
            ILogger<EditModel> logger)
        {
            _documentService = documentService;
            _teamService = teamService;
            _workspaceService = workspaceService;
            _userManager = userManager;
            _logger = logger;
        }

        public bool CanEdit { get; set; }
        public CollabDoc Document { get; set; }
        public int TeamId { get; set; }

        public ApplicationUser CurrentUser { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }
            CurrentUser = user;

            // Get document
            Document = await _documentService.GetDocumentByIdAsync(id);
            if (Document == null)
            {
                return NotFound($"Document with ID '{id}' not found.");
            }

            // Get workspace and team information
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(Document.WorkspaceId);
            if (workspace == null)
            {
                return NotFound($"Workspace not found.");
            }

            TeamId = workspace.TeamId;

            // Check if user has permission to view the document
            bool canView = await _documentService.UserCanViewDocumentAsync(id, user.Id);
            if (!canView)
            {
                return Forbid();
            }
            // CanEdit = await _documentService.UserCanEditDocumentAsync(id, user.Id);
            CanEdit = true;

            return Page();
        }
    }
}