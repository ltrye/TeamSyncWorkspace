using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;
using File = TeamSyncWorkspace.Models.File;

namespace TeamSyncWorkspace.Pages.Files
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly FolderService _folderService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FileService _fileService;

        public IndexModel(FolderService folderService, UserManager<ApplicationUser> userManager, FileService fileService)
        {
            _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        }

        [BindProperty(SupportsGet = true)]
        public string WorkspaceId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FolderId { get; set; }

        public Folder CurrentFolder { get; set; }
        public List<Folder> SubFolders { get; set; }
        public List<File> Files { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrEmpty(WorkspaceId))
                return BadRequest("Workspace ID is required.");

            CurrentFolder = FolderId.HasValue
                ? await _folderService.GetFolderAsync(FolderId.Value)
                : await _folderService.EnsureRootFolderAsync(WorkspaceId, user.Id);

            if (CurrentFolder == null)
            {
                return NotFound("Folder not found.");
            }

            SubFolders = await _folderService.GetSubFoldersAsync(CurrentFolder.FolderId);
            Files = await _fileService.GetFilesAsync(CurrentFolder.FolderId);

            return Page();
        }

        [BindProperty]
        public string NewFolderName { get; set; }

        public async Task<IActionResult> OnPostCreateFolderAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(NewFolderName))
            {
                ModelState.AddModelError(string.Empty, "Folder name is required.");
                return await OnGetAsync();
            }

            if (string.IsNullOrEmpty(WorkspaceId))
            {
                return BadRequest("Workspace ID is missing.");
            }

            CurrentFolder = FolderId.HasValue
                ? await _folderService.GetFolderAsync(FolderId.Value)
                : await _folderService.EnsureRootFolderAsync(WorkspaceId, user.Id);

            if (CurrentFolder == null)
            {
                return NotFound("Parent folder not found.");
            }

            await _folderService.CreateSubFolderAsync(WorkspaceId, NewFolderName, CurrentFolder.FolderId, user.Id);

            return RedirectToPage(new { WorkspaceId, FolderId = CurrentFolder.FolderId });
        }

        public async Task<IActionResult> OnPostDeleteFolderByIdAsync(int folderId, string workspaceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrEmpty(workspaceId))
            {
                return BadRequest("Workspace ID is required.");
            }

            WorkspaceId = workspaceId;

            // Get the folder before deleting it to retrieve the ParentFolderId
            var folder = await _folderService.GetFolderAsync(folderId);
            if (folder == null)
            {
                return NotFound("Folder not found.");
            }

            int? parentFolderId = folder.ParentFolderId; // Store parent folder ID before deleting

            var success = await _folderService.DeleteFolderAsync(folderId);
            if (!success)
            {
                return NotFound("Folder not found.");
            }

            // Redirect to the parent folder, or root if parentFolderId is null
            return RedirectToPage(new { WorkspaceId, FolderId = parentFolderId });
        }


        [BindProperty]
        public IFormFile UploadedFile { get; set; }

        public async Task<IActionResult> OnPostUploadFileAsync()
        {
            if (UploadedFile == null || FolderId == null)
            {
                ModelState.AddModelError(string.Empty, "No file selected or folder ID missing.");
                return await OnGetAsync();
            }

            bool success = await _fileService.UploadFileAsync(UploadedFile, FolderId.Value);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "File upload failed.");
            }

            return RedirectToPage(new { WorkspaceId, FolderId });
        }

        public async Task<IActionResult> OnPostDeleteFileAsync(int fileId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (fileId <= 0)
            {
                return BadRequest("Invalid file ID.");
            }

            bool success = await _fileService.DeleteFileAsync(fileId);
            if (!success)
            {
                return NotFound("File not found or deletion failed.");
            }

            return RedirectToPage(new { WorkspaceId, FolderId });
        }

    }
}
