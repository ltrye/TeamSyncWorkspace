using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Files
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly FolderService _folderService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(FolderService folderService, UserManager<ApplicationUser> userManager)
        {
            _folderService = folderService;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string WorkspaceId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FolderId { get; set; } // Allow navigating into subfolders

        public Folder CurrentFolder { get; set; }
        public List<Folder> SubFolders { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge(); // Redirect to login if not authenticated
            }

            if (string.IsNullOrEmpty(WorkspaceId))
                return BadRequest("Workspace ID is required.");

            // If FolderId is null, get the root folder
            CurrentFolder = FolderId.HasValue
                ? await _folderService.GetFolderAsync(FolderId.Value)
                : await _folderService.EnsureRootFolderAsync(WorkspaceId, user.Id);

            if (CurrentFolder == null)
            {
                return NotFound("Folder not found.");
            }

            // Get all subfolders within this folder
            SubFolders = await _folderService.GetSubFoldersAsync(CurrentFolder.FolderId);

            return Page();
        }

        [BindProperty]
        public string NewFolderName { get; set; } // Add this
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

            //  Ensure WorkspaceId is set
            if (string.IsNullOrEmpty(WorkspaceId))
            {
                return BadRequest("Workspace ID is missing.");
            }

            //  Ensure CurrentFolder is set before using it
            CurrentFolder = FolderId.HasValue
                ? await _folderService.GetFolderAsync(FolderId.Value)
                : await _folderService.EnsureRootFolderAsync(WorkspaceId, user.Id);

            if (CurrentFolder == null)
            {
                return NotFound("Parent folder not found.");
            }

            //  Debugging log to ensure values are correct
            Console.WriteLine($"Creating Folder: Name={NewFolderName}, ParentId={CurrentFolder.FolderId}, WorkspaceId={WorkspaceId}");

            await _folderService.CreateSubFolderAsync(
                workspaceId: WorkspaceId,  //  Ensure this is not null!
                folderName: NewFolderName,
                parentFolderId: CurrentFolder.FolderId,
                createdByUserId: user.Id
            );

            return RedirectToPage(new { WorkspaceId, FolderId = CurrentFolder.FolderId }); // Refresh the page
        }
    }
}
