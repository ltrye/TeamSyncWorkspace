using Microsoft.AspNetCore.Mvc;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Controllers
{
    [Route("api/folders")]
    [ApiController]
    public class FolderApiController : ControllerBase
    {
        private readonly FolderService _folderService;

        public FolderApiController(FolderService folderService)
        {
            _folderService = folderService;
        }

        //Check root to use/create
        [HttpPost("root")]
        public async Task<IActionResult> EnsureRootFolder([FromQuery] string workspaceId, [FromQuery] int createdByUserId)
        {
            if (string.IsNullOrEmpty(workspaceId))
                return BadRequest("Workspace ID is required");

            var folder = await _folderService.EnsureRootFolderAsync(workspaceId, createdByUserId);
            return Ok(folder);
        }

        //Create sub
        [HttpPost("create")]
        public async Task<IActionResult> CreateSubFolder([FromBody] FolderCreateRequest request)
        {
            if (string.IsNullOrEmpty(request.WorkspaceId) || string.IsNullOrWhiteSpace(request.FolderName))
                return BadRequest("Workspace ID and folder name are required");

            try
            {
                var folder = await _folderService.CreateSubFolderAsync(request.WorkspaceId, request.FolderName, request.ParentFolderId, request.CreatedByUserId);
                return Ok(folder);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Retrieve all folders
        [HttpGet]
        public async Task<IActionResult> GetFolders([FromQuery] string workspaceId)
        {
            if (string.IsNullOrEmpty(workspaceId))
                return BadRequest("Workspace ID is required");

            var folders = await _folderService.GetFoldersAsync(workspaceId);
            return Ok(folders);
        }
    }

    //DTO for security
    public class FolderCreateRequest
    {
        public string WorkspaceId { get; set; }
        public string FolderName { get; set; }
        public int ParentFolderId { get; set; }
        public int CreatedByUserId { get; set; }
    }
}
