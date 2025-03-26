using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly DocumentService _documentService;
        private readonly WorkspaceService _workspaceService;
        private readonly TeamService _teamService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            DocumentService documentService,
            TeamService teamService,
            UserManager<ApplicationUser> userManager,
            WorkspaceService workspaceService,
            ILogger<DocumentsController> logger)
        {
            _documentService = documentService;
            _teamService = teamService;
            _userManager = userManager;
            _logger = logger;
            _workspaceService = workspaceService;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateContent(int id, [FromBody] UpdateDocumentContentModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Check if user has permission to edit
            bool canEdit = await _documentService.UserCanEditDocumentAsync(id, user.Id);
            if (!canEdit)
            {
                return Forbid();
            }

            await _documentService.UpdateDocumentContentAsync(id, model.Content, user.Id);
            return Ok();
        }

        [HttpPut("{id}/title")]
        public async Task<IActionResult> UpdateTitle(int id, [FromBody] UpdateDocumentTitleModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Check if user has permission to edit
            bool canEdit = await _documentService.UserCanEditDocumentAsync(id, user.Id);
            if (!canEdit)
            {
                return Forbid();
            }

            await _documentService.UpdateDocumentTitleAsync(id, model.Title, user.Id);
            return Ok();
        }

        [HttpGet("{id}/permissions")]
        public async Task<IActionResult> GetPermissions(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(int.Parse(id));
            if (document == null)
            {
                return NotFound();
            }

            // Check if user is document owner or team admin
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(document.WorkspaceId);
            bool isOwnerOrAdmin = document.CreatedByUserId == user.Id ||
                                 await _teamService.IsUserTeamAdminAsync(workspace.TeamId, user.Id);

            if (!isOwnerOrAdmin)
            {
                return Forbid();
            }

            var permissions = await _documentService.GetDocumentPermissionsAsync(id);
            return Ok(permissions);
        }

        [HttpPost("{id}/permissions")]
        public async Task<IActionResult> SetPermission(string id, [FromBody] DocumentPermissionModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(int.Parse(id));
            if (document == null)
            {
                return NotFound();
            }

            // Check if user is document owner or team admin
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(document.WorkspaceId);
            bool isOwnerOrAdmin = document.CreatedByUserId == user.Id ||
                                 await _teamService.IsUserTeamAdminAsync(workspace.TeamId, user.Id);

            if (!isOwnerOrAdmin)
            {
                return Forbid();
            }

            await _documentService.SetDocumentPermissionAsync(id, model.UserId, model.CanEdit);
            return Ok();
        }

        [HttpGet("{id}/operations")]
        public async Task<IActionResult> GetOperations(int id, [FromQuery] string since)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Check if user has permission to view
            bool canView = await _documentService.UserCanViewDocumentAsync(id, user.Id);
            if (!canView)
            {
                return Forbid();
            }

            DateTime sinceTimestamp = DateTime.MinValue;
            if (DateTime.TryParse(since, out var parsedTime))
            {
                sinceTimestamp = parsedTime;
            }

            var operations = await _documentService.GetDocumentOperationsAsync(id, sinceTimestamp);
            return Ok(operations);
        }

        [HttpPost("{id}/operations")]
        public async Task<IActionResult> AddOperation(int id, [FromBody] DocumentOperationModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Check if user has permission to edit
            bool canEdit = await _documentService.UserCanEditDocumentAsync(id, user.Id);
            if (!canEdit)
            {
                return Forbid();
            }

            await _documentService.TrackDocumentOperationAsync(id, user.Id, model.OperationType, model.OperationData);
            return Ok();
        }

        [HttpGet("{id}/collaborators")]
        public async Task<IActionResult> GetCollaborators(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Check if user has permission to view
            bool canView = await _documentService.UserCanViewDocumentAsync(id, user.Id);
            if (!canView)
            {
                return Forbid();
            }

            var collaborators = await _documentService.GetActiveCollaboratorsAsync(id);
            return Ok(collaborators.Select(c => new
            {
                id = c.Id,
                name = $"{c.FirstName} {c.LastName}",
                email = c.Email
            }));
        }

        [HttpGet("{id}/export/pdf")]
        public async Task<IActionResult> ExportAsPdf(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Check if user has permission to view
            bool canView = await _documentService.UserCanViewDocumentAsync(id, user.Id);
            if (!canView)
            {
                return Forbid();
            }

            var pdfData = await _documentService.ExportDocumentAsPdfAsync(id);
            return File(pdfData, "application/pdf", $"{document.Title}.pdf");
        }

        [HttpGet("{id}/export/docx")]
        public async Task<IActionResult> ExportAsDocx(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Check if user has permission to view
            bool canView = await _documentService.UserCanViewDocumentAsync(id, user.Id);
            if (!canView)
            {
                return Forbid();
            }

            var docxData = await _documentService.ExportDocumentAsDocxAsync(id);
            return File(docxData, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{document.Title}.docx");
        }
    }

    public class UpdateDocumentContentModel
    {
        public string Content { get; set; }
    }

    public class UpdateDocumentTitleModel
    {
        public string Title { get; set; }
    }

    public class DocumentPermissionModel
    {
        public int UserId { get; set; }
        public bool CanEdit { get; set; }
    }

    public class DocumentOperationModel
    {
        public string OperationType { get; set; }
        public string OperationData { get; set; }
    }
}